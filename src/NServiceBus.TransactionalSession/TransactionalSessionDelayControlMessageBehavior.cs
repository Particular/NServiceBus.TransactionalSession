namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using DelayedDelivery;
    using System.Threading.Tasks;
    using Logging;
    using Pipeline;
    using Routing;
    using Transport;

    class TransactionalSessionDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;

        public TransactionalSessionDelayControlMessageBehavior(IMessageDispatcher dispatcher, string physicalQueueAddress)
        {
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            var isCommitControlMessage = context.Message.Headers.ContainsKey(OutboxTransactionalSession.RemainingCommitDurationHeaderName);

            if (isCommitControlMessage == false)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var remainingCommitDuration = TimeSpan.Parse(context.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName]);
            var commitDelayIncrement = TimeSpan.Parse(context.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName]);

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
                        new OutgoingMessage(messageId, headers, ReadOnlyMemory<byte>.Empty),
                        new UnicastAddressTag(physicalQueueAddress),
                        new DispatchProperties
                        {
                            DelayDeliveryWith = new DelayDeliveryWith(commitDelayIncrement)
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