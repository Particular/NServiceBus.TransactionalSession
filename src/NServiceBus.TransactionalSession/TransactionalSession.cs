namespace NServiceBus.TransactionalSession
{
    using Extensibility;
    using Outbox;
    using Persistence;
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class TransactionalSession : ITransactionalSession
    {
        readonly IOutboxStorage outboxStorage;
        readonly PendingTransportOperations pendingOperations;
        readonly ContextBag context;
        IOutboxTransaction outboxTransaction;
        readonly ICompletableSynchronizedStorageSession synchronizedStorageSession;
        readonly MessageSessionHolder messageSessionHolder;
        TransportTransaction transportTransaction;
        bool isSessionOpen = false;

        public TransactionalSession(IOutboxStorage outboxStorage, ICompletableSynchronizedStorageSession synchronizedStorageSession, MessageSessionHolder messageSessionHolder)
        {
            this.outboxStorage = outboxStorage;
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.messageSessionHolder = messageSessionHolder;
            pendingOperations = new PendingTransportOperations();
            context = new ContextBag();
            transportTransaction = new TransportTransaction();
            context.Set(pendingOperations);
        }

        public async Task Open(CancellationToken cancellationToken = default)
        {
            outboxTransaction = await outboxStorage.BeginTransaction(context, cancellationToken);
            await synchronizedStorageSession.Open(outboxTransaction, transportTransaction, context, cancellationToken);
            isSessionOpen = true;
        }

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSessionHolder.Instance.Send(message, sendOptions, cancellationToken);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSessionHolder.Instance.Send(messageConstructor, sendOptions, cancellationToken);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSessionHolder.Instance.Publish(message, publishOptions, cancellationToken);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!isSessionOpen)
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSessionHolder.Instance.Publish(messageConstructor, publishOptions, cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task Subscribe(Type eventType, SubscribeOptions subscribeOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task Unsubscribe(Type eventType, UnsubscribeOptions unsubscribeOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            // send control messages (1 per pending operation? one for all?)
            // convert pending operations to outbox record
            // store outbox record
            // complete/dispose outbox tx
            // dispatch the pending operation
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!isSessionOpen)
                    throw new InvalidOperationException("Before accessing the SynchronizedStorageSession, make sure to open the session by calling the `Open`-method.");

                return synchronizedStorageSession;
            }
        }

        public string SessionId { get; }
    }
}