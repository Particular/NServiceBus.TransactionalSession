namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    class BatchedMessageSession : IBatchedMessageSession
    {
        public BatchedMessageSession(
            IMessageSession messageSession,
            IMessageDispatcher dispatcher)
        {
            this.messageSession = messageSession;
            this.dispatcher = dispatcher;
        }

        public virtual async Task Commit(CancellationToken cancellationToken = default) =>
            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);

        public virtual async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            sendOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public virtual async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            publishOptions.GetExtensions().Set(pendingOperations);
            await messageSession.Publish(messageConstructor, publishOptions, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
        }

        readonly IMessageDispatcher dispatcher;
        readonly PendingTransportOperations pendingOperations = new PendingTransportOperations();
        readonly IMessageSession messageSession;
    }
}