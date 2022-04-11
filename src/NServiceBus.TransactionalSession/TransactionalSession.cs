namespace NServiceBus.TransactionalSession
{
    using Persistence;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class TransactionalSession : ITransactionalSession
    {
        public Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe(Type eventType, SubscribeOptions subscribeOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Unsubscribe(Type eventType, UnsubscribeOptions unsubscribeOptions, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ISynchronizedStorageSession SynchronizedStorageSession { get; }
        public string SessionId { get; }
    }
}