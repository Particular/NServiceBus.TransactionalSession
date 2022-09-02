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
        }

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SynchronizedStorageSession.");
                }

                return synchronizedStorageSession;
            }
        }

        public string SessionId
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SessionId.");
                }

                return options?.SessionId;
            }
        }

        protected ContextBag Context => options.Extensions;

        protected bool IsOpen => options != null;

        public async Task Commit(CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            await CommitInternal(cancellationToken).ConfigureAwait(false);

            committed = true;
        }


        protected abstract Task CommitInternal(CancellationToken cancellationToken = default);

        public virtual Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfCommitted();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
            }

            this.options = options;
            return Task.CompletedTask;
        }

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(messageConstructor, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(Dispose));
            }
        }

        void ThrowIfCommitted()
        {
            if (committed)
            {
                throw new InvalidOperationException("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.");
            }
        }

        void ThrowIfNotOpened()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("This session has not been opened yet.");
            }
        }

        void ThrowIfInvalidState()
        {
            ThrowIfDisposed();
            ThrowIfCommitted();
            ThrowIfNotOpened();
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
        protected OpenSessionOptions options;
        readonly IMessageSession messageSession;
        protected bool disposed;
        protected bool committed;
    }
}