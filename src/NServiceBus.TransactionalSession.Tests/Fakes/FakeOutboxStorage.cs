namespace NServiceBus.TransactionalSession.Tests.Fakes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;

    class FakeOutboxStorage : IOutboxStorage
    {
        public List<(OutboxMessage outboxMessage, OutboxTransaction transaction, ContextBag context)> Stored { get; } = new();
        public Action<OutboxMessage, OutboxTransaction, ContextBag> StoreCallback { get; set; } = null;

        public List<FakeOutboxTransaction> StartedTransactions { get; } = new();

        public Task<OutboxMessage> Get(string messageId, ContextBag context) => throw new NotImplementedException();

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
            Stored.Add((message, transaction, context));
            StoreCallback?.Invoke(message, transaction, context);

            return Task.CompletedTask;
        }

        public Task SetAsDispatched(string messageId, ContextBag context) =>
            throw new NotImplementedException();

        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            var tx = new FakeOutboxTransaction();
            StartedTransactions.Add(tx);
            return Task.FromResult<OutboxTransaction>(tx);
        }
    }
}