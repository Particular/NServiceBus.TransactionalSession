namespace NServiceBus.TransactionalSession
{
    using Persistence;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITransactionalSession : IMessageSession, IDisposable
    {
        /// <summary>
        /// Gets the synchronized storage session for processing the current message. NServiceBus makes sure the changes made
        /// via this session will be persisted before the message receive is acknowledged.
        /// </summary>
        ISynchronizedStorageSession SynchronizedStorageSession { get; }

        string SessionId { get; }

        // Name super temporary
        Task Commit(CancellationToken cancellationToken = default);
    }
}