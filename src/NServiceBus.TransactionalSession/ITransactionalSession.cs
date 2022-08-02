namespace NServiceBus.TransactionalSession
{
    using Persistence;
    using Extensibility;

    /// <summary>
    /// A transactional session that provides basic message operations. 
    /// </summary>
    public interface ITransactionalSession : IBatchSession
    {
        /// <summary>

        /// <summary>
        /// Enables passing persister-specific data between <see cref="TransactionalSessionFeature"/> and
        /// persister-specific open methods <see cref="OpenSessionExtensions"/>.
        /// </summary>
        internal ContextBag PersisterSpecificOptions { get; }
        /// Gets the synchronized storage session for processing the current message. NServiceBus makes sure the changes made
        /// via this session will be persisted before the message receive is acknowledged.
        /// </summary>
        ISynchronizedStorageSession SynchronizedStorageSession { get; }
    }
}