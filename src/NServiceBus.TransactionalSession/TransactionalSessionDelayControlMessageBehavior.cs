namespace NServiceBus.TransactionalSession;

using System;
using System.Threading.Tasks;
using DelayedDelivery;
using Logging;
using Pipeline;
using Routing;
using Transport;

class TransactionalSessionDelayControlMessageBehavior(IMessageDispatcher dispatcher,
    string physicalQueueAddress, TransactionalSessionMetrics metrics)
    : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
{
    public async Task Invoke(IIncomingPhysicalMessageContext context,
        Func<IIncomingPhysicalMessageContext, Task> next)
    {
        if (!context.Extensions.TryGet<DispatchMessage>(out var dispatchMessage))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var remainingCommitDuration = dispatchMessage.RemainingCommitDuration;
        var commitDelayIncrement = dispatchMessage.CommitDelayIncrement;
        int currentAttempt = dispatchMessage.Attempt;
        var nextAttempt = currentAttempt + 1;

        metrics.RecordTransitTime(dispatchMessage.TimeSent);

        var messageId = context.MessageId;
        if (remainingCommitDuration <= TimeSpan.Zero)
        {
            if (Log.IsWarnEnabled)
            {
                Log.WarnFormat(
                    "Consuming transaction commit control message for message ID '{0}' because the maximum commit duration has elapsed. If this occurs repeatedly, consider increasing the {1} setting on the session.", messageId, nameof(OpenSessionOptions.MaximumCommitDuration));
            }

            metrics.RecordControlMessageOutcome(currentAttempt, false);

            return;
        }

        var newCommitDelay = commitDelayIncrement.Add(commitDelayIncrement);
        commitDelayIncrement = newCommitDelay > remainingCommitDuration ? remainingCommitDuration : newCommitDelay;

        var actualElapsed = dispatchMessage.TimeSent.HasValue
            ? DateTimeOffset.UtcNow - dispatchMessage.TimeSent.Value
            : commitDelayIncrement;
        TimeSpan newRemainingTime = remainingCommitDuration - actualElapsed;

        if (Log.IsInfoEnabled)
        {
            Log.InfoFormat(
                "Delaying transaction commit control message for message ID '{0}' by {1} seconds. If this occurs repeatedly for the same message id, consider increasing the {2} setting on the session.",
                messageId, Math.Abs(commitDelayIncrement.TotalSeconds), nameof(OpenSessionOptions.CommitDelayIncrement));
        }

        var headers = context.Message.ToHeaders(nextAttempt, newRemainingTime, commitDelayIncrement);

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