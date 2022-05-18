namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Outbox;
    using Pipeline;
    using Routing;
    using Transport;

    class TransactionalSessionDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        readonly IOutboxStorage outboxStorage;
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;
        TimeSpan MaxCommitDelay = TimeSpan.FromSeconds(15);
        TimeSpan CommitDelayIncrement = TimeSpan.FromSeconds(5);

        public TransactionalSessionDelayControlMessageBehavior(IOutboxStorage outboxStorage, IMessageDispatcher dispatcher, string physicalQueueAddress)
        {
            this.outboxStorage = outboxStorage;
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

            var messageId = context.MessageId;

            var outboxRecord = await outboxStorage.Get(messageId, context.Extensions, CancellationToken.None).ConfigureAwait(false);
            var transactionCommitted = outboxRecord != null;
            var timeSinceCommitStart = DateTimeOffset.UtcNow.Subtract(commitStartedAt);

            if (transactionCommitted || timeSinceCommitStart > MaxCommitDelay)
            {
                await next().ConfigureAwait(false);
                return;
            }

            Log.Debug($"Delaying transaction commit control messages for messageId={messageId}");

            await dispatcher.Dispatch(new TransportOperations(
                    new Transport.TransportOperation(
                        new OutgoingMessage(messageId, context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), ReadOnlyMemory<byte>.Empty),
                        new UnicastAddressTag(physicalQueueAddress),
                        new DispatchProperties(new Dictionary<string, string>
                        {
                            {Headers.DeliverAt, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow.Add(CommitDelayIncrement))},
                        }),
                        DispatchConsistency.Isolated
                    )
                ), new TransportTransaction(), context.CancellationToken)
                .ConfigureAwait(false);

            throw new ConsumeMessageException();
        }

        static ILog Log = LogManager.GetLogger<TransactionalSessionDelayControlMessageBehavior>();

    }
}