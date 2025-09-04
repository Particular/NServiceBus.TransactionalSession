namespace NServiceBus.TransactionalSession;

using System;

// This is a class because a struct would get boxed in the extension bag anyway
sealed class DispatchMessage(
    int attempt,
    TimeSpan commitDelayIncrement,
    TimeSpan remainingCommitDuration,
    DateTimeOffset? timeSent = null)
{
    public readonly int Attempt = attempt;
    public readonly TimeSpan CommitDelayIncrement = commitDelayIncrement;
    public readonly TimeSpan RemainingCommitDuration = remainingCommitDuration;
    public readonly DateTimeOffset? TimeSent = timeSent;
}