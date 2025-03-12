namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Outbox;
using TransactionalSession;

sealed class CustomTestingOutboxTransaction : IOutboxTransaction
{
    public TaskCompletionSource<bool> CommitTaskCompletionSource { get; set; } = null;

    public CustomTestingOutboxTransaction(ContextBag contextBag)
    {
        if (contextBag.TryGet(out CustomTestingPersistenceOpenSessionOptions options))
        {
            CommitTaskCompletionSource = options.TransactionCommitTaskCompletionSource;
        }

        Transaction = new AcceptanceTestingTransaction();
    }

    public AcceptanceTestingTransaction Transaction { get; private set; }

    public void Dispose()
    {
        Transaction = null;
    }

    public async Task Commit(CancellationToken cancellationToken = default)
    {
        if (CommitTaskCompletionSource != null)
        {
            await CommitTaskCompletionSource.Task.ConfigureAwait(false);
        }

        Transaction.Commit();
    }

    public void Enlist(Action action)
    {
        Transaction.Enlist(action);
    }
}