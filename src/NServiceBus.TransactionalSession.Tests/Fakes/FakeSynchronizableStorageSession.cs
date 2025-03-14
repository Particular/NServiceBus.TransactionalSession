namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Outbox;
using Persistence;
using Transport;

class FakeSynchronizableStorageSession : ICompletableSynchronizedStorageSession
{
    public List<(IOutboxTransaction, ContextBag)> OpenedOutboxTransactionSessions { get; } = [];
    public List<ContextBag> OpenedTransactionSessions { get; } = [];
    public Func<IOutboxTransaction, ContextBag, bool> TryOpenCallback { get; set; }
    public Action CompleteCallback { get; set; } = null;
    public bool Completed { get; private set; }
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;

    public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = new())
    {
        if (transaction == null)
        {
            return new ValueTask<bool>(false);
        }

        OpenedOutboxTransactionSessions.Add((transaction, context));
        return new ValueTask<bool>(TryOpenCallback?.Invoke(transaction, context) ?? true);
    }

    public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
        CancellationToken cancellationToken = new CancellationToken()) => new(false);

    public Task Open(ContextBag contextBag, CancellationToken cancellationToken = new())
    {
        OpenedTransactionSessions.Add(contextBag);
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = new())
    {
        Completed = true;
        CompleteCallback?.Invoke();

        return Task.CompletedTask;
    }
}