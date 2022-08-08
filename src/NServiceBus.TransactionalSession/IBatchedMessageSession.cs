namespace NServiceBus.TransactionalSession;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A session that provides basic message operations that will be batched together.
/// </summary>
public interface IBatchedMessageSession : IDisposable
{
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
    /// Commit the session by applying all message and synchronized storage operation in an atomic manner.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task Commit(CancellationToken cancellationToken = default);
}