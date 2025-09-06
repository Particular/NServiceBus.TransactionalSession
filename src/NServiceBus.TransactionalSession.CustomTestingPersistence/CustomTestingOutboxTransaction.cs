namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Logging;
using Outbox;
using TransactionalSession;

sealed class CustomTestingOutboxTransaction : IOutboxTransaction
{
    TaskCompletionSource<bool> CommitTaskCompletionSource { get; }

    public CustomTestingOutboxTransaction(ContextBag contextBag)
    {
        context = contextBag;

        if (contextBag.TryGet(out CustomTestingPersistenceOpenSessionOptions options))
        {
            CommitTaskCompletionSource = options.TransactionCommitTaskCompletionSource;
        }

        Transaction = new AcceptanceTestingTransaction();
    }

    public AcceptanceTestingTransaction Transaction { get; private set; }

    public void Dispose()
    {
        if (Transaction == null)
        {
            return;
        }

        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.TransactionDispose", logContext ?? "Pipeline");

        Transaction = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public async Task Commit(CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.TransactionCommit", logContext ?? "Pipeline");

        if (CommitTaskCompletionSource != null)
        {
            await CommitTaskCompletionSource.Task.ConfigureAwait(false);
        }

        Transaction.Commit();
    }

    public void Enlist(Action action) => Transaction.Enlist(action);

    readonly ContextBag context;

    static readonly ILog Logger = LogManager.GetLogger<CustomTestingOutboxStorage>();
}