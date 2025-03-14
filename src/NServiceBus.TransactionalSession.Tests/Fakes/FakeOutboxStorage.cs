namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Outbox;

class FakeOutboxStorage : IOutboxStorage
{
    public List<(OutboxMessage outboxMessage, IOutboxTransaction transaction, ContextBag context)> Stored { get; } = [];
    public List<(string messageId, ContextBag context)> Dispatched { get; } = [];
    public Action<OutboxMessage, IOutboxTransaction, ContextBag> StoreCallback { get; init; }

    Action<string, ContextBag> DispatchedCallback => null;

    public List<FakeOutboxTransaction> StartedTransactions { get; } = [];

    public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        Stored.Add((message, transaction, context));
        StoreCallback?.Invoke(message, transaction, context);

        return Task.CompletedTask;
    }

    public Task SetAsDispatched(string messageId, ContextBag context,
        CancellationToken cancellationToken = default)
    {
        Dispatched.Add((messageId, context));
        DispatchedCallback?.Invoke(messageId, context);

        return Task.CompletedTask;
    }

    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = default)
    {
        var tx = new FakeOutboxTransaction();
        StartedTransactions.Add(tx);
        return Task.FromResult<IOutboxTransaction>(tx);
    }
}