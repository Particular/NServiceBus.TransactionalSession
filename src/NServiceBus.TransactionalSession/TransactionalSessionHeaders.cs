namespace NServiceBus.TransactionalSession;

/// <summary>
/// 
/// </summary>
public class TransactionalSessionHeaders
{
    /// <summary>
    /// 
    /// </summary>
    public const string OriginatingEndpoint = "NServiceBus.TransactionalSession.OriginEndpoint";

    internal const string RemainingCommitDuration = "NServiceBus.TransactionalSession.RemainingCommitDuration";

    internal const string CommitDelayIncrementHeader = "NServiceBus.TransactionalSession.CommitDelayIncrement";
}