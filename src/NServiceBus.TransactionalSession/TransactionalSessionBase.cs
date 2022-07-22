namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;
    using Transport;

    abstract class TransactionalSessionBase : ITransactionalSession
    {
        protected TransactionalSessionBase(
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher)
        {
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
            pendingOperations = new PendingTransportOperations();
            transportTransaction = new TransportTransaction();
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
        public abstract Task Commit(CancellationToken cancellationToken = default);

        public virtual Task Open(ContextBag context = null, CancellationToken cancellationToken = default)
        {
            this.context = context ?? new ContextBag();
            SessionId = Guid.NewGuid().ToString();
            isSessionOpen = true;
            return Task.CompletedTask;
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

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                synchronizedStorageSession?.Dispose();
            }

            disposed = true;
        }

        protected readonly ICompletableSynchronizedStorageSession synchronizedStorageSession;
        protected readonly IMessageDispatcher dispatcher;
        protected readonly PendingTransportOperations pendingOperations;
        internal ContextBag context;
        protected readonly TransportTransaction transportTransaction;
        readonly IMessageSession messageSession;
        bool isSessionOpen;
        bool disposed;
    }
}