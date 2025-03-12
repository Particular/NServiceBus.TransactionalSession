namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System.Threading;
using System.Threading.Tasks;
using Outbox;

class FakeOutboxTransaction : IOutboxTransaction
{
    public bool Committed { get; private set; }
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;

    public Task Commit(CancellationToken cancellationToken = new())
    {
        Committed = true;
        return Task.CompletedTask;
    }
}