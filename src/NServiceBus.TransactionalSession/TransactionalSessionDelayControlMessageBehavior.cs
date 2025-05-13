namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using DelayedDelivery;
using System.Threading.Tasks;
using Logging;
using Pipeline;
using Routing;
using Transport;

class TransactionalSessionDelayControlMessageBehavior(IMessageDispatcher dispatcher,
    string physicalQueueAddress)
    : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
{
    public async Task Invoke(IIncomingPhysicalMessageContext context,
        Func<IIncomingPhysicalMessageContext, Task> next)
    {
        if (!context.Message.Headers.TryGetValue(OutboxTransactionalSession.RemainingCommitDurationHeaderName,
                out var remainingCommitDurationHeader)
            || !context.Message.Headers.TryGetValue(OutboxTransactionalSession.CommitDelayIncrementHeaderName,
                out var commitDelayIncrementHeader))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var remainingCommitDuration = TimeSpan.Parse(remainingCommitDurationHeader);
        var commitDelayIncrement = TimeSpan.Parse(commitDelayIncrementHeader);

        var messageId = context.MessageId;
        if (remainingCommitDuration <= TimeSpan.Zero)
        {
            if (Log.IsWarnEnabled)
            {
                Log.WarnFormat(
                    "Consuming transaction commit control message for message ID '{0}' because the maximum commit duration has elapsed. If this occurs repeatedly, consider increasing the {1} setting on the session.", messageId, nameof(OpenSessionOptions.MaximumCommitDuration));
            }

            return;
        }

        var newCommitDelay = commitDelayIncrement.Add(commitDelayIncrement);
        commitDelayIncrement = newCommitDelay > remainingCommitDuration ? remainingCommitDuration : newCommitDelay;

        if (Log.IsInfoEnabled)
        {
            Log.InfoFormat(
                "Delaying transaction commit control message for message ID '{0}' by {1} seconds. If this occurs repeatedly for the same message id, consider increasing the {2} setting on the session.",
                messageId, Math.Abs(commitDelayIncrement.TotalSeconds), nameof(OpenSessionOptions.CommitDelayIncrement));
        }

        var headers = new Dictionary<string, string>(context.Message.Headers)
        {
            [OutboxTransactionalSession.RemainingCommitDurationHeaderName] =
                (remainingCommitDuration - commitDelayIncrement).ToString(),
            [OutboxTransactionalSession.CommitDelayIncrementHeaderName] = commitDelayIncrement.ToString()
        };
        await dispatcher.Dispatch(new TransportOperations(
                new TransportOperation(
                    new OutgoingMessage(messageId, headers, ReadOnlyMemory<byte>.Empty),
                    new UnicastAddressTag(physicalQueueAddress),
                    new DispatchProperties { DelayDeliveryWith = new DelayDeliveryWith(commitDelayIncrement) },
                    DispatchConsistency.Isolated
                )
            ), new TransportTransaction(), context.CancellationToken)
            .ConfigureAwait(false);

        throw new ConsumeMessageException();
    }

    static readonly ILog Log = LogManager.GetLogger<TransactionalSessionDelayControlMessageBehavior>();
}