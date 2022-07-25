namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Outbox;

class FakeOutboxStorage : IOutboxStorage
{
    public List<(OutboxMessage outboxMessage, IOutboxTransaction transaction, ContextBag context)> Stored { get; } = new();
    public Action<OutboxMessage, IOutboxTransaction, ContextBag> StoreCallback { get; set; } = null;

    public List<FakeOutboxTransaction> StartedTransactions { get; } = new();

    public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

    public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context,
        CancellationToken cancellationToken = new CancellationToken())
    {
        Stored.Add((message, transaction, context));
        StoreCallback?.Invoke(message, transaction, context);

        return Task.CompletedTask;
    }

    public Task SetAsDispatched(string messageId, ContextBag context,
        CancellationToken cancellationToken = new CancellationToken()) =>
        throw new NotImplementedException();

    public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = new CancellationToken())
    {
        var tx = new FakeOutboxTransaction();
        StartedTransactions.Add(tx);
        return Task.FromResult<IOutboxTransaction>(tx);
    }
}