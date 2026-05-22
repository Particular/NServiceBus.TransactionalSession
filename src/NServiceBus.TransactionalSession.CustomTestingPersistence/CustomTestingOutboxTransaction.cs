namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Microsoft.Extensions.Logging;
using Outbox;
using TransactionalSession;

sealed class CustomTestingOutboxTransaction : IOutboxTransaction
{
    TaskCompletionSource<bool> CommitTaskCompletionSource { get; }

    public CustomTestingOutboxTransaction(ContextBag contextBag, ILogger logger)
    {
        context = contextBag;
        this.logger = logger;

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
        logger.LogInformation("{LogContext} - Outbox.TransactionDispose", logContext ?? "Pipeline");

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
        logger.LogInformation("{LogContext} - Outbox.TransactionCommit", logContext ?? "Pipeline");

        if (CommitTaskCompletionSource != null)
        {
            await CommitTaskCompletionSource.Task.ConfigureAwait(false);
        }

        Transaction.Commit();
    }

    public void Enlist(Action action) => Transaction.Enlist(action);

    readonly ContextBag context;
    readonly ILogger logger;
}