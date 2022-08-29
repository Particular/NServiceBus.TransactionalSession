namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using DelayedDelivery;
    using System.Threading.Tasks;
    using DeliveryConstraints;
    using Extensibility;
    using Logging;
    using Pipeline;
    using Routing;
    using Transport;

    class TransactionalSessionDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        public TransactionalSessionDelayControlMessageBehavior(IDispatchMessages dispatcher, string physicalQueueAddress)
        {
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            if (!context.Message.Headers.TryGetValue(OutboxTransactionalSession.RemainingCommitDurationHeaderName, out var remainingCommitDurationHeader)
                || !context.Message.Headers.TryGetValue(OutboxTransactionalSession.CommitDelayIncrementHeaderName, out var commitDelayIncrementHeader))
            {
                await next().ConfigureAwait(false);
                return;
            }

            var remainingCommitDuration = TimeSpan.Parse(remainingCommitDurationHeader);
            var commitDelayIncrement = TimeSpan.Parse(commitDelayIncrementHeader);

            var messageId = context.MessageId;
            if (remainingCommitDuration <= TimeSpan.Zero)
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

            var newCommitDelay = commitDelayIncrement.Add(commitDelayIncrement);
            commitDelayIncrement = newCommitDelay > remainingCommitDuration ? remainingCommitDuration : newCommitDelay;

            var headers = new Dictionary<string, string>(context.Message.Headers)
            {
                [OutboxTransactionalSession.RemainingCommitDurationHeaderName] = (remainingCommitDuration - commitDelayIncrement).ToString(),
                [OutboxTransactionalSession.CommitDelayIncrementHeaderName] = commitDelayIncrement.ToString()
            };
            await dispatcher.Dispatch(new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(messageId, headers, Array.Empty<byte>()),
                        new UnicastAddressTag(physicalQueueAddress),
                        DispatchConsistency.Isolated,
                        new List<DeliveryConstraint>
                        {
                            new DelayDeliveryWith(commitDelayIncrement)
                        }
                    )
                ), new TransportTransaction(), new ContextBag())
                .ConfigureAwait(false);

            throw new ConsumeMessageException();
        }

        readonly IDispatchMessages dispatcher;
        readonly string physicalQueueAddress;
        static readonly ILog Log = LogManager.GetLogger<TransactionalSessionDelayControlMessageBehavior>();
    }
}