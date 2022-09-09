namespace NServiceBus.TransactionalSession.Tests.Fakes
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class FakeDispatcher : IDispatchMessages
    {
        public List<(TransportOperations outgoingMessages, TransportTransaction transaction)> Dispatched = new();

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
        {
            Dispatched.Add((outgoingMessages, transaction));
            return Task.CompletedTask;
        }
    }
}