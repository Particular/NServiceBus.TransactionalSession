namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Logging;
using Outbox;
using Persistence;
using TransactionalSession;
using Transport;

sealed class CustomTestingSynchronizedStorageSession : ICompletableSynchronizedStorageSession
{
    AcceptanceTestingTransaction Transaction { get; set; }

    public void Dispose() => Transaction = null;

    public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        context.TryGet(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out logContext);
        Logger.InfoFormat("{0} - StorageSession.TryOpen(OutboxTransaction)", logContext ?? "Pipeline");

        if (transaction is CustomTestingOutboxTransaction inMemOutboxTransaction)
        {
            Transaction = inMemOutboxTransaction.Transaction;
            ownsTransaction = false;
            return new ValueTask<bool>(true);
        }

        return new ValueTask<bool>(false);
    }

    public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        context.TryGet(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out logContext);
        Logger.InfoFormat("{0} - StorageSession.TryOpen(TransportTransaction)", logContext ?? "Pipeline");

        if (!transportTransaction.TryGet(out Transaction ambientTransaction))
        {
            return new ValueTask<bool>(false);
        }

        Transaction = new AcceptanceTestingTransaction();
        ambientTransaction.EnlistVolatile(new EnlistmentNotification(Transaction), EnlistmentOptions.None);
        ownsTransaction = true;
        return new ValueTask<bool>(true);
    }

    public Task Open(ContextBag contextBag, CancellationToken cancellationToken = default)
    {
        contextBag.TryGet(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out logContext);
        Logger.InfoFormat("{0} - StorageSession.Open", logContext ?? "Pipeline");

        ownsTransaction = true;
        Transaction = new AcceptanceTestingTransaction();
        return Task.CompletedTask;
    }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        Logger.InfoFormat("{0} - StorageSession.CompleteAsync", logContext ?? "Pipeline");

        if (ownsTransaction)
        {
            Transaction.Commit();
        }

        return Task.CompletedTask;
    }

    public void Enlist(Action action) => Transaction.Enlist(action);

    bool ownsTransaction;

    sealed class EnlistmentNotification(AcceptanceTestingTransaction transaction) : IEnlistmentNotification
    {
        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            try
            {
                transaction.Commit();
                preparingEnlistment.Prepared();
            }
            catch (Exception ex)
            {
                preparingEnlistment.ForceRollback(ex);
            }
        }

        public void Commit(Enlistment enlistment) => enlistment.Done();

        public void Rollback(Enlistment enlistment)
        {
            transaction.Rollback();
            enlistment.Done();
        }

        public void InDoubt(Enlistment enlistment) => enlistment.Done();
    }

    string logContext;
    static readonly ILog Logger = LogManager.GetLogger<CustomTestingSynchronizedStorageSession>();
}