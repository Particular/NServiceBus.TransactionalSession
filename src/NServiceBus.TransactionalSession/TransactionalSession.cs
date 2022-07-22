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
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher) : base(synchronizedStorageSession, messageSession, dispatcher)
        {
        }

        public override async Task Commit(CancellationToken cancellationToken = default)
        {
            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
        }

        public override async Task Open(ContextBag context = null, CancellationToken cancellationToken = default)
        {
            await base.Open(context, cancellationToken).ConfigureAwait(false);

            await synchronizedStorageSession.Open(null, transportTransaction, this.context, cancellationToken).ConfigureAwait(false);
        }
    }
}