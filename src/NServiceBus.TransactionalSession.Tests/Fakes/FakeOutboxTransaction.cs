namespace NServiceBus.TransactionalSession.Tests.Fakes
{
    using System.Threading.Tasks;
    using Outbox;

    class FakeOutboxTransaction : OutboxTransaction
    {
        public bool Commited { get; private set; }

        public void Dispose() { }

        public Task Commit()
        {
            Commited = true;
            return Task.CompletedTask;
        }
    }
}