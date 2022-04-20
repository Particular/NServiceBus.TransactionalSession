namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Persistence;
    using Routing;
    using Transport;
    using TransportTransportOperation = Transport.TransportOperation;
    using OutboxTransportOperation = Outbox.TransportOperation;

    class OutboxTransactionalSession : ITransactionalSession
    {
        public OutboxTransactionalSession(
            IOutboxStorage outboxStorage,
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher,
            string physicalQueueAddress)
        {
            this.outboxStorage = outboxStorage;
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
            pendingOperations = new PendingTransportOperations();
            outboxTransactionContext = new ContextBag();
        }

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(messageConstructor, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            var message = new OutgoingMessage(SessionId, new Dictionary<string, string>
            {
                { Headers.ControlMessageHeader, bool.TrueString },
                { "", "" }
            }, ReadOnlyMemory<byte>.Empty);

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(physicalQueueAddress)));
            await dispatcher.Dispatch(outgoingMessages, transportTransaction, cancellationToken).ConfigureAwait(false);

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, outboxTransactionContext, cancellationToken)
                .ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            synchronizedStorageSession.Dispose();
            outboxTransaction?.Dispose();
        }

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!isSessionOpen)
                {
                    throw new InvalidOperationException(
                        "Before accessing the SynchronizedStorageSession, make sure to open the session by calling the `Open`-method.");
                }

                return synchronizedStorageSession;
            }
        }

        public string SessionId { get; private set; }

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

        public async Task Open(CancellationToken cancellationToken = default)
        {
            transportTransaction = new TransportTransaction();
            outboxTransaction = await outboxStorage.BeginTransaction(outboxTransactionContext, cancellationToken).ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, outboxTransactionContext, cancellationToken).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister is not compatible.");
            }

            SessionId = Guid.NewGuid().ToString();
            isSessionOpen = true;
        }

        readonly ContextBag outboxTransactionContext;
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;

        readonly IOutboxStorage outboxStorage;
        readonly PendingTransportOperations pendingOperations;
        readonly ICompletableSynchronizedStorageSession synchronizedStorageSession;
        readonly IMessageSession messageSession;
        TransportTransaction transportTransaction;
        bool isSessionOpen;

        IOutboxTransaction outboxTransaction;
    }
}