namespace NServiceBus.AcceptanceTesting;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Extensibility;
using Logging;
using Outbox;
using Pipeline;
using TransactionalSession;

sealed class CustomTestingOutboxStorage(CustomTestingDatabase database, string endpointName) : IOutboxStorage
{
    public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
    {
        context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
        Logger.InfoFormat("{0} - Outbox.Get", logContext ?? "Pipeline");

        if (context.TryGet("TestOutboxStorage.GetResult", out OutboxMessage customResult))
        {
            return Task.FromResult(customResult);
        }

        var owningEndpointName = GetOriginEndpointName(context);

        var recordId = GetOutboxRecordId(messageId, owningEndpointName);

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
            var recordId = GetOutboxRecordId(message.MessageId, endpointName);

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

        var owningEndpointName = GetOriginEndpointName(context);

        var recordId = GetOutboxRecordId(messageId, owningEndpointName);

        if (!database.TryGetValue(recordId, out var storedMessage))
        {
            return Task.CompletedTask;
        }

        storedMessage.MarkAsDispatched();
        return Task.CompletedTask;
    }

    string GetOutboxRecordId(string messageId, string owningEndpoint) => $"{owningEndpoint}-{messageId}";

    string GetOriginEndpointName(ContextBag context)
    {
        if (context is not ITransportReceiveContext receiveContext)
        {
            throw new InvalidOperationException("Getting the Originating endpoint name requires ITransportReceiveContext.");
        }

        return receiveContext.Message.Headers[Headers.OriginatingEndpoint];
    }

    static readonly Task<OutboxMessage> NoOutboxMessageTask = Task.FromResult(default(OutboxMessage));

    static readonly ILog Logger = LogManager.GetLogger<CustomTestingOutboxStorage>();
}