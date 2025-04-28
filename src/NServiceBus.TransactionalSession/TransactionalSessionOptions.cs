namespace NServiceBus.TransactionalSession;

using Extensibility;

/// <summary>
/// Configuration options for the transactional session.
/// </summary>
public class TransactionalSessionOptions : ExtendableOptions
{
    /// <summary>
    /// Address of the endpoint that will process the control messages used to dispatch outbox records.
    /// </summary>
    public string ProcessorAddress { get; init; }
}
