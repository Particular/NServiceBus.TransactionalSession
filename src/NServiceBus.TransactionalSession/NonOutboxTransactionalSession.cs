namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;
    using Transport;

    sealed class NonOutboxTransactionalSession : TransactionalSessionBase
    {
        public NonOutboxTransactionalSession(
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher,
            IEnumerable<IOpenSessionOptionsCustomization> customizations) : base(synchronizedStorageSession, messageSession, dispatcher, customizations)
        {
        }

        protected override async Task CommitInternal(CancellationToken cancellationToken = default)
        {
            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
        }

        public override async Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
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

            await synchronizedStorageSession.Open(null, new TransportTransaction(), Context, cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                synchronizedStorageSession.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}