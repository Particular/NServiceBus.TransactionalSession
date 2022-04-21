namespace NServiceBus.TransactionalSession
{
    using Persistence;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ITransactionalSession : IDisposable
    {
        /// <summary>
        /// Opens the transaction session.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A <see cref="CancellationToken"/> to observe.</returns>
        Task Open(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the provided message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="sendOptions">The options for the send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Instantiates a message of type T and sends it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="sendOptions">The options for the send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publish the message to subscribers.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="publishOptions">The options for the publish.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Instantiates a message of type T and publishes it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="publishOptions">Specific options for this event.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default);

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