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

    class OutboxTransactionalSession : TransactionalSessionBase
    {
        public OutboxTransactionalSession(
            IOutboxStorage outboxStorage,
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher,
            string physicalQueueAddress) : base(synchronizedStorageSession, messageSession, dispatcher)
        {
            this.outboxStorage = outboxStorage;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Commit(CancellationToken cancellationToken = default)
        {
            var message = new OutgoingMessage(SessionId, new Dictionary<string, string>
            {
                { Headers.MessageId, SessionId },
                { Headers.ControlMessageHeader, bool.TrueString },
                { ControlMessageSentAtHeaderName, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) }
            }, ReadOnlyMemory<byte>.Empty);

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(physicalQueueAddress)));
            await dispatcher.Dispatch(outgoingMessages, transportTransaction, cancellationToken).ConfigureAwait(false);

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, contextBag, cancellationToken)
                .ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                outboxTransaction?.Dispose();
            }
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

        public override async Task Open(CancellationToken cancellationToken = default)
        {
            await base.Open(cancellationToken).ConfigureAwait(false);

            outboxTransaction = await outboxStorage.BeginTransaction(contextBag, cancellationToken).ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, contextBag, cancellationToken).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister is not compatible.");
            }
        }

        readonly string physicalQueueAddress;
        readonly IOutboxStorage outboxStorage;
        IOutboxTransaction outboxTransaction;
        public const string ControlMessageSentAtHeaderName = "NServiceBus.TransactionalSession.CommitStartedAt";
    }
}