namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Transport;

static class IncomingMessageExtensions
{
    public static bool TryGetDispatchMessage(this IncomingMessage message, [NotNullWhen(true)] out DispatchMessage? controlMessage)
    {
        if (!message.Headers.TryGetValue(OutboxTransactionalSession.RemainingCommitDurationHeaderName,
                out var remainingCommitDurationHeader)
            || !message.Headers.TryGetValue(OutboxTransactionalSession.CommitDelayIncrementHeaderName,
                out var commitDelayIncrementHeader))
        {
            controlMessage = null;
            return false;
        }

        int attempt = message.Headers.TryGetValue(OutboxTransactionalSession.AttemptHeaderName, out var attemptHeaderValue)
            ? int.Parse(attemptHeaderValue)
            : 1;

        DateTimeOffset? timeSent = message.Headers.TryGetValue(OutboxTransactionalSession.TimeSentHeaderName,
            out var timeSentHeader)
            ? DateTimeOffsetHelper.ToDateTimeOffset(timeSentHeader)
            : null;

        var remainingCommitDuration = TimeSpan.Parse(remainingCommitDurationHeader);
        var commitDelayIncrement = TimeSpan.Parse(commitDelayIncrementHeader);
        controlMessage = new DispatchMessage(attempt, commitDelayIncrement, remainingCommitDuration, timeSent);
        return true;
    }

    public static Dictionary<string, string> ToHeaders(this IncomingMessage message, int attempt, TimeSpan remainingCommitDuration, TimeSpan commitDelayIncrement)
    {
        var headers = new Dictionary<string, string>(message.Headers)
        {
            [OutboxTransactionalSession.AttemptHeaderName] = attempt.ToString(),
            [OutboxTransactionalSession.RemainingCommitDurationHeaderName] = remainingCommitDuration.ToString(),
            [OutboxTransactionalSession.CommitDelayIncrementHeaderName] = commitDelayIncrement.ToString(),
            [OutboxTransactionalSession.TimeSentHeaderName] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow)
        };
        return headers;
    }
}