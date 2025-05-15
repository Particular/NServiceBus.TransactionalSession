namespace NServiceBus.AcceptanceTesting;

using System.Collections.Concurrent;
using Outbox;

public class CustomTestingDatabase
{
    public bool TryGetValue(string recordId, out StoredMessage o) => storage.TryGetValue(recordId, out o);

    public bool TryAdd(string recordId, StoredMessage storedMessage) => storage.TryAdd(recordId, storedMessage);

    readonly ConcurrentDictionary<string, StoredMessage> storage = new();

    public class StoredMessage(string messageId, string endpointName, TransportOperation[] transportOperations)
    {
        string Id { get; } = messageId;
        string EndpointName { get; } = endpointName;

        bool Dispatched { get; set; }

        public TransportOperation[] TransportOperations { get; private set; } = transportOperations;

        public void MarkAsDispatched()
        {
            Dispatched = true;
            TransportOperations = [];
        }

        bool Equals(StoredMessage other) => string.Equals(Id, other.Id) && string.Equals(EndpointName, other.EndpointName) && Dispatched.Equals(other.Dispatched);

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

            return obj.GetType() == GetType() && Equals((StoredMessage)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Id?.GetHashCode() ?? 0) * 397) ^ ((EndpointName?.GetHashCode() ?? 0) * 397) ^ Dispatched.GetHashCode();
            }
        }
    }
}