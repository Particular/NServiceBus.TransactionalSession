namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;

    public class CustomTestingOutboxTransaction : OutboxTransaction
    {
        public const string TransactionCommitTCSKey = "TestingTransport.TxCommitTCS";

        public TaskCompletionSource<bool> CommitTaskCompletionSource { get; set; } = null;

        public CustomTestingOutboxTransaction(ContextBag contextBag)
        {
            if (contextBag.TryGet(TransactionCommitTCSKey, out TaskCompletionSource<bool> tcs))
            {
                CommitTaskCompletionSource = tcs;
            }

            Transaction = new AcceptanceTestingTransaction();
        }

        public AcceptanceTestingTransaction Transaction { get; private set; }

        public void Dispose()
        {
            Transaction = null;
        }

        public async Task Commit()
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