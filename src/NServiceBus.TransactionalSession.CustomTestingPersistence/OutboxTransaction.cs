namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;

    class OutboxTransaction : IOutboxTransaction
    {
        public TaskCompletionSource<bool> CommitTaskCompletionSource { get; set; } = null;

        public OutboxTransaction(ContextBag contextBag)
        {
            if (contextBag.TryGet(out CustomTestingPersistenceOpenSessionOptions options))
            {
                CommitTaskCompletionSource = options.TransactionCommitTaskCompletionSource;
            }

            Transaction = new Transaction();
        }

        public Transaction Transaction { get; private set; }

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
}