namespace NServiceBus.TransactionalSession;

/// <summary>
/// Configuration options for the transactional session.
/// </summary>
public class TransactionalSessionOptions
{
    /// <summary>
    /// Address of the endpoint that will process the control messages used to dispatch outbox records.
    /// </summary>
    public string ProcessorAddress { get; init; }
}
