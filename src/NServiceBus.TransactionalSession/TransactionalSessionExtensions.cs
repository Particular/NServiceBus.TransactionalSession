namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Extensions for the <see cref="BatchedMessageSession"/>
    /// </summary>
    public static class TransactionalSessionExtensions
    {
        /// <summary>
        /// Sends the provided message.
        /// </summary>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task Send(this IBatchedMessageSession session, object message, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(message), message);

            return session.Send(message, new SendOptions(), cancellationToken);
        }

        /// <summary>
        /// Instantiates a message of <typeparamref name="T" /> and sends it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <remarks>
        /// The message will be sent to the destination configured for <typeparamref name="T" />.
        /// </remarks>
        public static Task Send<T>(this IBatchedMessageSession session, Action<T> messageConstructor, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(messageConstructor), messageConstructor);

            return session.Send(messageConstructor, new SendOptions(), cancellationToken);
        }

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="destination">The address of the destination to which the message will be sent.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task Send(this IBatchedMessageSession session, string destination, object message, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNullAndEmpty(nameof(destination), destination);
            Guard.AgainstNull(nameof(message), message);

            var options = new SendOptions();

            options.SetDestination(destination);

            return session.Send(message, options, cancellationToken);
        }

        /// <summary>
        /// Instantiates a message of type T and sends it to the given destination.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="destination">The destination to which the message will be sent.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task Send<T>(this IBatchedMessageSession session, string destination, Action<T> messageConstructor, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNullAndEmpty(nameof(destination), destination);
            Guard.AgainstNull(nameof(messageConstructor), messageConstructor);

            var options = new SendOptions();

            options.SetDestination(destination);

            return session.Send(messageConstructor, options, cancellationToken);
        }

        /// <summary>
        /// Sends the message back to the current endpoint. Shortcut for <see cref="RoutingOptionExtensions.RouteToThisEndpoint(SendOptions)">sendOptions.RouteToThisEndpoint()</see>.
        /// </summary>
        /// <param name="session">Object being extended.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task SendLocal(this IBatchedMessageSession session, object message, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(message), message);

            var options = new SendOptions();

            options.RouteToThisEndpoint();

            return session.Send(message, options, cancellationToken);
        }

        /// <summary>
        /// Instantiates a message of type T and sends it back to the current endpoint. Shortcut for <see cref="RoutingOptionExtensions.RouteToThisEndpoint(SendOptions)">sendOptions.RouteToThisEndpoint()</see>.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="session">Object being extended.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task SendLocal<T>(this IBatchedMessageSession session, Action<T> messageConstructor, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(messageConstructor), messageConstructor);

            var options = new SendOptions();

            options.RouteToThisEndpoint();

            return session.Send(messageConstructor, options, cancellationToken);
        }

        /// <summary>
        /// Publish the message to subscribers.
        /// </summary>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="message">The message to publish.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task Publish(this IBatchedMessageSession session, object message, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(message), message);

            return session.Publish(message, new PublishOptions(), cancellationToken);
        }

        /// <summary>
        /// Publish the message to subscribers.
        /// </summary>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <typeparam name="T">The message type.</typeparam>
        public static Task Publish<T>(this IBatchedMessageSession session, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);

            return session.Publish<T>(_ => { }, new PublishOptions(), cancellationToken);
        }

        /// <summary>
        /// Instantiates a message of type T and publishes it.
        /// </summary>
        /// <typeparam name="T">The type of message, usually an interface.</typeparam>
        /// <param name="session">The instance of <see cref="IBatchedMessageSession" /> to use for the action.</param>
        /// <param name="messageConstructor">An action which initializes properties of the message.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        public static Task Publish<T>(this IBatchedMessageSession session, Action<T> messageConstructor, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(nameof(session), session);
            Guard.AgainstNull(nameof(messageConstructor), messageConstructor);

            return session.Publish(messageConstructor, new PublishOptions(), cancellationToken);
        }
    }
}