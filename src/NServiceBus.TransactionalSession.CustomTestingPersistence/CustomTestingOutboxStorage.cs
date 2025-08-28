namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Logging;
using Outbox;
using TransactionalSession;

sealed class CustomTestingOutboxStorage(CustomTestingDatabase database, string endpointName) : IOutboxStorage
{
    public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.Get", logContext ?? "Pipeline");

        if (context.TryGet<CustomTestingOutboxStorageResult>(out var customResult))
        {
            return Task.FromResult(customResult.Message);
        }

        var recordId = GetOutboxRecordId(messageId);

        if (database.TryGetValue(recordId, out var storedMessage))
        {
            return Task.FromResult(new OutboxMessage(recordId, storedMessage.TransportOperations));
        }

        return NoOutboxMessageTask;
    }

    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.BeginTransaction", logContext ?? "Pipeline");

        if (context.TryGet(out CustomTestingPersistenceOpenSessionOptions options) && options.UseTransactionScope)
        {
            _ = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);
        }

        return Task.FromResult<IOutboxTransaction>(new CustomTestingOutboxTransaction(context));
    }

    public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context, CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.Store", logContext ?? "Pipeline");

        var tx = (CustomTestingOutboxTransaction)transaction;
        tx.Enlist(() =>
        {
            var recordId = GetOutboxRecordId(message.MessageId);

            if (!database.TryAdd(recordId, new CustomTestingDatabase.StoredMessage(message.MessageId, endpointName, message.TransportOperations)))
            {
                throw new Exception($"Outbox message with id '{message.MessageId}' associated to endpoint {endpointName} is already present in storage.");
            }

            if (context.TryGet("TestOutboxStorage.StoreCallback", out Action callback))
            {
                callback();
            }
        });
        return Task.CompletedTask;
    }

    public Task SetAsDispatched(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.SetAsDispatched", logContext ?? "Pipeline");

        var recordId = GetOutboxRecordId(messageId);

        if (!database.TryGetValue(recordId, out var storedMessage))
        {
            return Task.CompletedTask;
        }

        storedMessage.MarkAsDispatched();
        return Task.CompletedTask;
    }

    string GetOutboxRecordId(string messageId) => $"{endpointName}-{messageId}";

    static readonly Task<OutboxMessage> NoOutboxMessageTask = Task.FromResult(default(OutboxMessage));

    static readonly ILog Logger = LogManager.GetLogger<CustomTestingOutboxStorage>();
}