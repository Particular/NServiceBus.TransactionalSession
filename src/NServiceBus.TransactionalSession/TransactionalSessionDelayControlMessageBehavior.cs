namespace NServiceBus.TransactionalSession
{
    using System;
    using DelayedDelivery;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;
    using Pipeline;
    using Routing;
    using Transport;

    class TransactionalSessionDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;
        TimeSpan MaxCommitDelay = TimeSpan.FromSeconds(15);
        TimeSpan CommitDelayIncrement = TimeSpan.FromSeconds(5);

        public TransactionalSessionDelayControlMessageBehavior(IMessageDispatcher dispatcher, string physicalQueueAddress)
        {
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            var isCommitControlMessage = context.Message.Headers.ContainsKey(OutboxTransactionalSession.ControlMessageSentAtHeaderName);

            if (isCommitControlMessage == false)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var commitStartedAtText = context.Message.Headers[OutboxTransactionalSession.ControlMessageSentAtHeaderName];
            var commitStartedAt = DateTimeOffsetHelper.ToDateTimeOffset(commitStartedAtText);

            var timeSinceCommitStart = DateTimeOffset.UtcNow.Subtract(commitStartedAt);

            var messageId = context.MessageId;
            if (timeSinceCommitStart > MaxCommitDelay)
            {
                if (Log.IsInfoEnabled)
                {
                    Log.Info($"Consuming transaction commit control messages for messageId={messageId} to create the outbox tomb stone.");
                }
                return;
            }

            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Delaying transaction commit control messages for messageId={messageId}");
            }

            await dispatcher.Dispatch(new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(messageId, context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), ReadOnlyMemory<byte>.Empty),
                        new UnicastAddressTag(physicalQueueAddress),
                        new DispatchProperties
                        {
                            DelayDeliveryWith = new DelayDeliveryWith(CommitDelayIncrement)
                        },
                        DispatchConsistency.Isolated
                    )
                ), new TransportTransaction(), context.CancellationToken)
                .ConfigureAwait(false);

            throw new ConsumeMessageException();
        }

        static readonly ILog Log = LogManager.GetLogger<TransactionalSessionDelayControlMessageBehavior>();
    }
}