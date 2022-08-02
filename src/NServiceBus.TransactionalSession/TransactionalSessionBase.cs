namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    abstract class TransactionalSessionBase
    {
        protected TransactionalSessionBase(
            IMessageSession messageSession,
            IMessageDispatcher dispatcher)
        {
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
            pendingOperations = new PendingTransportOperations();
        }

        public string SessionId => options?.SessionId;

        protected ContextBag Context => options.Extensions;

        protected bool IsOpen => options != null;

        public abstract Task Commit(CancellationToken cancellationToken = default);

        protected virtual Task OpenSession(OpenSessionOptions options = null, CancellationToken cancellationToken = default)
        {
            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. Open should only be called once.");
            }

            this.options = options ?? new OpenSessionOptions();
            return Task.CompletedTask;
        }

        ContextBag ITransactionalSession.PersisterSpecificOptions { get; } = new ContextBag();

        public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
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

            disposed = true;
        }

        protected readonly IMessageDispatcher dispatcher;
        protected readonly PendingTransportOperations pendingOperations;
        protected OpenSessionOptions options;
        readonly IMessageSession messageSession;
        protected bool disposed;
    }
}