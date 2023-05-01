namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using Outbox;
    using Performance.TimeToBeReceived;
    using Persistence;
    using Routing;
    using Transport;
    using TransportTransportOperation = Transport.TransportOperation;
    using OutboxTransportOperation = Outbox.TransportOperation;

    sealed class OutboxTransactionalSession : TransactionalSessionBase
    {
        public OutboxTransactionalSession(IOutboxStorage outboxStorage,
                                          CompletableSynchronizedStorageSessionAdapter synchronizedStorageSession,
                                          IMessageSession messageSession,
                                          IDispatchMessages dispatcher,
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
            var message = new OutgoingMessage(SessionId, headers, Array.Empty<byte>());

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(physicalQueueAddress)));
            await dispatcher.Dispatch(outgoingMessages, new TransportTransaction(), new ContextBag()).ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync().ConfigureAwait(false);

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, Context)
                .ConfigureAwait(false);

            await outboxTransaction.Commit().ConfigureAwait(false);
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
                var options = new Dictionary<string, string>();

                foreach (var constraint in operation.DeliveryConstraints)
                {
                    SerializeDeliveryConstraint(constraint, options);
                }

                SerializeRoutingStrategy(operation.AddressTag, options);

                transportOperations[index] = new OutboxTransportOperation(operation.Message.MessageId, options, operation.Message.Body, operation.Message.Headers);
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

        static void SerializeDeliveryConstraint(DeliveryConstraint constraint, Dictionary<string, string> options)
        {
            if (constraint is NonDurableDelivery)
            {
                options["NonDurable"] = true.ToString();
                return;
            }
            if (constraint is DoNotDeliverBefore doNotDeliverBefore)
            {
                options["DeliverAt"] = DateTimeExtensions.ToWireFormattedString(doNotDeliverBefore.At);
                return;
            }

            if (constraint is DelayDeliveryWith delayDeliveryWith)
            {
                options["DelayDeliveryFor"] = delayDeliveryWith.Delay.ToString();
                return;
            }

            if (constraint is DiscardIfNotReceivedBefore discard)
            {
                options["TimeToBeReceived"] = discard.MaxTime.ToString();
                return;
            }

            throw new Exception($"Unknown delivery constraint {constraint.GetType().FullName}");
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
            var outboxTransactionTask = outboxStorage.BeginTransaction(Context);
            return OpenInternal(outboxTransactionTask, cancellationToken);
        }

        async Task OpenInternal(Task<OutboxTransaction> beginTransactionTask, CancellationToken cancellationToken)
        {
            outboxTransaction = await beginTransactionTask.ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, Context).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister are not compatible.");
            }
        }

        readonly string physicalQueueAddress;
        readonly IOutboxStorage outboxStorage;
        OutboxTransaction outboxTransaction;
        public const string RemainingCommitDurationHeaderName = "NServiceBus.TransactionalSession.RemainingCommitDuration";
        public const string CommitDelayIncrementHeaderName = "NServiceBus.TransactionalSession.CommitDelayIncrement";
    }
}