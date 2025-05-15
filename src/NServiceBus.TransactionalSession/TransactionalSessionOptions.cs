namespace NServiceBus.TransactionalSession;

using Extensibility;

/// <summary>
/// Configuration options for the transactional session.
/// </summary>
public class TransactionalSessionOptions : ExtendableOptions
{
    /// <summary>
    /// The endpoint that will be responsible for dispatching outbox messages on behalf of this endpoint.
    /// </summary>
    public string ProcessorEndpoint { get; init; }
}
