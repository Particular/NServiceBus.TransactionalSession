namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System.Threading;
using System.Threading.Tasks;
using Outbox;

class FakeOutboxTransaction : IOutboxTransaction
{
    public bool Commited { get; private set; }

    public void Dispose() { }

    public Task Commit(CancellationToken cancellationToken = new CancellationToken())
    {
        Commited = true;
        return Task.CompletedTask;
    }
}