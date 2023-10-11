namespace NServiceBus.TransactionalSession.Tests.Fakes
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    class FakeDispatcher : IMessageDispatcher
    {
        public List<(TransportOperations outgoingMessages, TransportTransaction transaction)> Dispatched = [];

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Dispatched.Add((outgoingMessages, transaction));
            return Task.CompletedTask;
        }
    }
}