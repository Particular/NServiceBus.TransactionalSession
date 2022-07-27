namespace NServiceBus.TransactionalSession
{
    using Persistence;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A transactional session that provides basic message operations. 
    /// </summary>
    public interface ITransactionalSession : IDisposable
    {
        /// <summary>
        /// Opens the transaction session.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the provided message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="sendOptions">The options for the send.</param>
#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        Task Send(object message, SendOptions sendOptions);

        /// <summary>
        /// Instantiates a message of type T and sends it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="sendOptions">The options for the send.</param>
        Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions);

        /// <summary>
        /// Publish the message to subscribers.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="publishOptions">The options for the publish.</param>
        Task Publish(object message, PublishOptions publishOptions);

        /// <summary>
        /// Instantiates a message of type T and publishes it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="publishOptions">Specific options for this event.</param>
        Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions);
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext

        /// <summary>
        /// Gets the synchronized storage session for processing the current message. NServiceBus makes sure the changes made
        /// via this session will be persisted before the message receive is acknowledged.
        /// </summary>
        SynchronizedStorageSession SynchronizedStorageSession { get; }

        /// <summary>
        /// Transactional session globally unique identifier
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Commit the session by applying all message and synchronized storage operation in an atomic manner.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Commit(CancellationToken cancellationToken = default);
    }
}