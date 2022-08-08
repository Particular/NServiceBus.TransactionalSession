namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;

    /// <summary>
    /// A transactional session that provides basic message operations. 
    /// </summary>
    public interface ITransactionalSession : IBatchedMessageSession
    {
        /// <summary>
        /// Gets the synchronized storage session for this transactional session.
        /// Storage operations are committed atomically with the message operations executed on this session.
        /// </summary>
        ISynchronizedStorageSession SynchronizedStorageSession { get; }

        /// <summary>
        /// Transactional session globally unique identifier
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Opens the transactional session.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        internal Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default);

        internal ContextBag PersisterSpecificOptions { get; }
    }
}