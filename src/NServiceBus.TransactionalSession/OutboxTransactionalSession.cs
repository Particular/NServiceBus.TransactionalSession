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
    using NServiceBus.DelayedDelivery;
    using NServiceBus.Extensibility;

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
            var headers = new Dictionary<string, string>
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

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(physicalQueueAddress), new DispatchProperties { DelayDeliveryWith = new DelayDeliveryWith(TimeSpan.FromMinutes(1)) }));
            await dispatcher.Dispatch(outgoingMessages, new TransportTransaction(), cancellationToken).ConfigureAwait(false);

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, Context, cancellationToken)
                .ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);

            DispatchAndComplete(outboxMessage, Context, cancellationToken);
        }

        async void DispatchAndComplete(OutboxMessage outboxMessage, ContextBag context, CancellationToken cancellationToken)
        {
            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
            await outboxStorage.SetAsDispatched(outboxMessage.MessageId, context, cancellationToken).ConfigureAwait(false);
            // Ignore exceptions, better to log but no logger exists here (yet..)
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
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

        public override async Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            await base.Open(options, cancellationToken).ConfigureAwait(false);

            outboxTransaction = await outboxStorage.BeginTransaction(Context, cancellationToken).ConfigureAwait(false);

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