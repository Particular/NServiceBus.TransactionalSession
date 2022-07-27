namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;

    public class CustomTestingOutboxStorage : IOutboxStorage
    {
        public Task<OutboxMessage> Get(string messageId, ContextBag context)
        {
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

        public Task<OutboxTransaction> BeginTransaction(ContextBag context)
        {
            return Task.FromResult<OutboxTransaction>(new CustomTestingOutboxTransaction(context));
        }

        public Task Store(OutboxMessage message, OutboxTransaction transaction, ContextBag context)
        {
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

        public Task SetAsDispatched(string messageId, ContextBag context)
        {
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
    }
}