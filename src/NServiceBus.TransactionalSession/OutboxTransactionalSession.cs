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

    class OutboxTransactionalSession : TransactionalSessionBase
    {
        public OutboxTransactionalSession(
            IOutboxStorage outboxStorage,
            CompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IDispatchMessages dispatcher,
            string physicalQueueAddress,
            ITransactionalSessionExtension extension) : base(synchronizedStorageSession, messageSession, dispatcher, extension)
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

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, Context)
                .ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync().ConfigureAwait(false);

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

        public override async Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default)
        {
            await base.Open(options, cancellationToken).ConfigureAwait(false);

            outboxTransaction = await outboxStorage.BeginTransaction(Context).ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, Context).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister is not compatible.");
            }
        }

        readonly string physicalQueueAddress;
        readonly IOutboxStorage outboxStorage;
        OutboxTransaction outboxTransaction;
        public const string RemainingCommitDurationHeaderName = "NServiceBus.TransactionalSession.RemainingCommitDuration";
        public const string CommitDelayIncrementHeaderName = "NServiceBus.TransactionalSession.CommitDelayIncrement";
    }
}