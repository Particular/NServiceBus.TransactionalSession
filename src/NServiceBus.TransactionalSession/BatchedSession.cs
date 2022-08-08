namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class BatchedSession : TransactionalSessionBase, IBatchedMessageSession
    {
        public BatchedSession(
            IMessageSession messageSession,
            IMessageDispatcher dispatcher) : base(messageSession, dispatcher)
        {
        }

        public override async Task Commit(CancellationToken cancellationToken = default) =>
            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
    }
}