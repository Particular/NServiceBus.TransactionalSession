namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class TransactionalSession : TransactionalSessionBase, IBatchSession
    {
        public TransactionalSession(
            IMessageSession messageSession,
            IMessageDispatcher dispatcher) : base(messageSession, dispatcher)
        {
        }

        async Task IBatchSession.Open(OpenSessionOptions options, CancellationToken cancellationToken) =>
            await base.OpenSession(options, cancellationToken).ConfigureAwait(false);

        public override async Task Commit(CancellationToken cancellationToken = default) =>
            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
    }
}