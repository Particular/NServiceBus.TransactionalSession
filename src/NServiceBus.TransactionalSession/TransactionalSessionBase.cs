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


        public abstract Task Commit(CancellationToken cancellationToken = default);

        public virtual async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
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
        readonly IMessageSession messageSession;
        protected bool disposed;
    }
}