namespace NServiceBus.TransactionalSession
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;
    using Transport;

    class TransactionalSession : TransactionalSessionBase
    {
        public TransactionalSession(
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
            await base.Open(options, cancellationToken).ConfigureAwait(false);

            await synchronizedStorageSession.Open(null, new TransportTransaction(), Context, cancellationToken).ConfigureAwait(false);
        }
    }
}