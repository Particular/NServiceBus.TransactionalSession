namespace NServiceBus.AcceptanceTesting;

using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Outbox;
using Persistence;
using Transport;

class CustomTestingSynchronizedStorageAdapter : ISynchronizedStorageAdapter
{
    public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
    {
        if (transaction is CustomTestingOutboxTransaction inMemOutboxTransaction)
        {
            var session = new CustomTestingSynchronizedStorageSession(inMemOutboxTransaction);

            return Task.FromResult<CompletableSynchronizedStorageSession>(session);
        }

        return Task.FromResult<CompletableSynchronizedStorageSession>(null);
    }

    public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction, ContextBag context)
    {
        if (!transportTransaction.TryGet(out Transaction ambientTransaction))
        {
            return Task.FromResult<CompletableSynchronizedStorageSession>(null);
        }

        var session = new CustomTestingSynchronizedStorageSession(ambientTransaction);

        return Task.FromResult<CompletableSynchronizedStorageSession>(session);
    }
}