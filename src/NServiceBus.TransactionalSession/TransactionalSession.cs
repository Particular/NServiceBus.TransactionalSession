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

    class TransactionalSession : ITransactionalSession
    {
        public TransactionalSession(IOutboxStorage outboxStorage,
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            DumpingGround dumpingGround, IMessageDispatcher dispatcher)
        {
            this.outboxStorage = outboxStorage;
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.dumpingGround = dumpingGround;
            this.dispatcher = dispatcher;
            pendingOperations = new PendingTransportOperations();
            context = new ContextBag();
            transportTransaction = new TransportTransaction();
            context.Set(pendingOperations);
        }

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await dumpingGround.Instance.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await dumpingGround.Instance.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await dumpingGround.Instance.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await dumpingGround.Instance.Publish(messageConstructor, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            var message = new OutgoingMessage(SessionId, new Dictionary<string, string> { { Headers.ControlMessageHeader, bool.TrueString } }, ReadOnlyMemory<byte>.Empty);

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(dumpingGround.PhysicalQueueAddress)));
            await dispatcher.Dispatch(outgoingMessages, transportTransaction, cancellationToken).ConfigureAwait(false);

            if (dumpingGround.IsOutboxEnabled)
            {
                var outboxMessage =
                    new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
                await outboxStorage.Store(outboxMessage, outboxTransaction, context, cancellationToken)
                    .ConfigureAwait(false);

                await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

                context.Remove<IOutboxTransaction>();
                await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);
            }

            // complete/dispose outbox tx
            // dispatch the pending operation
        }

        public void Dispose() => throw new NotImplementedException();

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
            outboxTransaction = await outboxStorage.BeginTransaction(context, cancellationToken).ConfigureAwait(false);
            context.Set(outboxTransaction);

            await synchronizedStorageSession.Open(outboxTransaction, transportTransaction, context, cancellationToken).ConfigureAwait(false);
            SessionId = Guid.NewGuid().ToString();
            isSessionOpen = true;
        }

        readonly ContextBag context;
        readonly IMessageDispatcher dispatcher;
        readonly DumpingGround dumpingGround;

        readonly IOutboxStorage outboxStorage;
        readonly PendingTransportOperations pendingOperations;
        readonly ICompletableSynchronizedStorageSession synchronizedStorageSession;
        readonly TransportTransaction transportTransaction;
        bool isSessionOpen;

        IOutboxTransaction outboxTransaction;
    }
}