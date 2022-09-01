namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;
    using Transport;

    class TransactionalSession : TransactionalSessionBase
    {
        public TransactionalSession(
            CompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IDispatchMessages dispatcher,
            ITransactionalSessionExtension extension) : base(synchronizedStorageSession, messageSession, dispatcher, extension)
        {
        }

        protected override async Task CommitInternal(CancellationToken cancellationToken = default)
        {
            await synchronizedStorageSession.CompleteAsync().ConfigureAwait(false);

            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
        }

        public override async Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default)
        {
            await base.Open(options, cancellationToken).ConfigureAwait(false);

            await synchronizedStorageSession.Open(null, new TransportTransaction(), Context).ConfigureAwait(false);
        }
    }
}