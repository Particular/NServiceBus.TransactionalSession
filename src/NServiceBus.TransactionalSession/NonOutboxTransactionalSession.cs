namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;
    using Transport;

    sealed class NonOutboxTransactionalSession : TransactionalSessionBase
    {
        public NonOutboxTransactionalSession(
            CompletableSynchronizedStorageSessionAdapter synchronizedStorageSession,
            IMessageSession messageSession,
            IDispatchMessages dispatcher,
            IEnumerable<IOpenSessionOptionsCustomization> customizations) : base(synchronizedStorageSession, messageSession, dispatcher, customizations)
        {
        }

        protected override async Task CommitInternal(CancellationToken cancellationToken = default)
        {
            await synchronizedStorageSession.CompleteAsync().ConfigureAwait(false);

            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
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

            await synchronizedStorageSession.Open(null, new TransportTransaction(), Context).ConfigureAwait(false);
        }
    }
}