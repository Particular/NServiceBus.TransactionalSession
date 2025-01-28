namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Outbox;
    using Persistence;
    using Routing;
    using Transport;
    using TransportTransportOperation = Transport.TransportOperation;
    using OutboxTransportOperation = Outbox.TransportOperation;

    sealed class OutboxTransactionalSession : TransactionalSessionBase
    {
        public OutboxTransactionalSession(IOutboxStorage outboxStorage,
                                          ICompletableSynchronizedStorageSession synchronizedStorageSession,
                                          IMessageSession messageSession,
                                          IMessageDispatcher dispatcher,
                                          IEnumerable<IOpenSessionOptionsCustomization> customizations,
                                          string physicalQueueAddress) : base(synchronizedStorageSession, messageSession, dispatcher, customizations)
        {
            this.outboxStorage = outboxStorage;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        protected override async Task CommitInternal(CancellationToken cancellationToken = default)
        {
            if (pendingOperations.HasOperations)
            {
                await DispatchControlMessage(cancellationToken).ConfigureAwait(false);
            }

            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);
            // Disposing the session after complete to be compliant with the core behavior
            // in case complete throws the synchronized storage session will get disposed by the dispose or the container
            // disposing multiple times is safe
            synchronizedStorageSession.Dispose();

            // The outbox record is only stored when there are operations to store. When there are no operations
            // it doesn't make sense to store an empty outbox record and mark it as dispatched as long as we are not
            // exposing the possibility to set the session ID from the outside. When the session ID is random and
            // there are not outgoing messages there is no way to correlate the outbox record with the transactional
            // session call and therefore the cost storing (within tx) and setting as dispatched (outside tx)
            // the outbox record is not justified.
            if (pendingOperations.HasOperations)
            {
                var outboxMessage =
                    new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
                await outboxStorage.Store(outboxMessage, outboxTransaction, Context, cancellationToken)
                    .ConfigureAwait(false);
            }

            await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);
        }

        async Task DispatchControlMessage(CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>()
            {
                { Headers.MessageId, SessionId },
                { Headers.ControlMessageHeader, bool.TrueString },
                { RemainingCommitDurationHeaderName, options.MaximumCommitDuration.ToString() },
                { CommitDelayIncrementHeaderName, options.CommitDelayIncrement.ToString() },
            };
            if (options.HasMetadata)
            {
                foreach (KeyValuePair<string, string> keyValuePair in options.Metadata)
                {
                    headers.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            var message = new OutgoingMessage(SessionId, headers, ReadOnlyMemory<byte>.Empty);
            var operation = new TransportTransportOperation(
                message,
                new UnicastAddressTag(physicalQueueAddress),
                null,
                DispatchConsistency.Isolated // We do not want the this dispatch to enlist in any active transaction scope like we do want for the outbox operation
                );
            var outgoingMessages = new TransportOperations();
            await dispatcher.Dispatch(outgoingMessages, new TransportTransaction(operation), cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                synchronizedStorageSession.Dispose();
                outboxTransaction?.Dispose();
            }

            base.Dispose(disposing);
        }

        static OutboxTransportOperation[] ConvertToOutboxOperations(TransportTransportOperation[] operations)
        {
            var transportOperations = new OutboxTransportOperation[operations.Length];
            int index = 0;
            foreach (TransportTransportOperation operation in operations)
            {
                SerializeRoutingStrategy(operation.AddressTag, operation.Properties);

                transportOperations[index] = new OutboxTransportOperation(operation.Message.MessageId, operation.Properties, operation.Message.Body, operation.Message.Headers);
                index++;
            }

            return transportOperations;
        }

        static void SerializeRoutingStrategy(AddressTag addressTag, Dictionary<string, string> options)
        {
            if (addressTag is MulticastAddressTag indirect)
            {
                options["EventType"] = indirect.MessageType.AssemblyQualifiedName;
                return;
            }

            if (addressTag is UnicastAddressTag direct)
            {
                options["Destination"] = direct.Destination;
                return;
            }

            throw new Exception($"Unknown routing strategy {addressTag.GetType().FullName}");
        }

        public override Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfCommitted();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
            }

            this.options = options;

            foreach (var customization in customizations)
            {
                customization.Apply(this.options);
            }

            // Unfortunately this is the only way to make it possible for Transaction.Current to float up to the caller
            // to make sure SQLP and NHibernate work with the transaction scope
            var outboxTransactionTask = outboxStorage.BeginTransaction(Context, cancellationToken);
            return OpenInternal(outboxTransactionTask, cancellationToken);
        }

        async Task OpenInternal(Task<IOutboxTransaction> beginTransactionTask, CancellationToken cancellationToken)
        {
            outboxTransaction = await beginTransactionTask.ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, Context, cancellationToken).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister are not compatible.");
            }
        }

        readonly string physicalQueueAddress;
        readonly IOutboxStorage outboxStorage;
        IOutboxTransaction outboxTransaction;
        public const string RemainingCommitDurationHeaderName = "NServiceBus.TransactionalSession.RemainingCommitDuration";
        public const string CommitDelayIncrementHeaderName = "NServiceBus.TransactionalSession.CommitDelayIncrement";
    }
}