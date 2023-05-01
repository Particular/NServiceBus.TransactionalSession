namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;
    using Logging;
    using Outbox;
    using TransactionalSession;

    sealed class CustomTestingOutboxStorage : IOutboxStorage
    {
        public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = default)
        {
            context.TryGet<string>(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var logContext);
            Logger.InfoFormat("{0} - Outbox.Get", logContext ?? "Pipeline");

            if (context.TryGet("TestOutboxStorage.GetResult", out OutboxMessage customResult))
            {
                return Task.FromResult(customResult);
            }

            if (storage.TryGetValue(messageId, out var storedMessage))
            {
                return Task.FromResult(new OutboxMessage(messageId, storedMessage.TransportOperations));
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
                if (!storage.TryAdd(message.MessageId, new StoredMessage(message.MessageId, message.TransportOperations)))
                {
                    throw new Exception($"Outbox message with id '{message.MessageId}' is already present in storage.");
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

            if (!storage.TryGetValue(messageId, out var storedMessage))
            {
                return Task.CompletedTask;
            }

            storedMessage.MarkAsDispatched();
            return Task.CompletedTask;
        }

        public void RemoveEntriesOlderThan(DateTime dateTime)
        {
            foreach (var entry in storage)
            {
                var storedMessage = entry.Value;
                if (storedMessage.Dispatched && storedMessage.StoredAt < dateTime)
                {
                    storage.TryRemove(entry.Key, out _);
                }
            }
        }

        ConcurrentDictionary<string, StoredMessage> storage = new ConcurrentDictionary<string, StoredMessage>();

        static Task<OutboxMessage> NoOutboxMessageTask = Task.FromResult(default(OutboxMessage));

        class StoredMessage
        {
            public StoredMessage(string messageId, TransportOperation[] transportOperations)
            {
                TransportOperations = transportOperations;
                Id = messageId;
                StoredAt = DateTime.UtcNow;
            }

            public string Id { get; }

            public bool Dispatched { get; private set; }

            public DateTime StoredAt { get; }

            public TransportOperation[] TransportOperations { get; private set; }

            public void MarkAsDispatched()
            {
                Dispatched = true;
                TransportOperations = new TransportOperation[0];
            }

            protected bool Equals(StoredMessage other)
            {
                return string.Equals(Id, other.Id) && Dispatched.Equals(other.Dispatched);
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }
                if (ReferenceEquals(this, obj))
                {
                    return true;
                }
                if (obj.GetType() != GetType())
                {
                    return false;
                }
                return Equals((StoredMessage)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Id?.GetHashCode() ?? 0) * 397) ^ Dispatched.GetHashCode();
                }
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<CustomTestingOutboxStorage>();
    }
}