namespace NServiceBus.TransactionalSession;

using AcceptanceTesting;
using Configuration.AdvancedExtensibility;
using Features;

/// <summary>
/// Enables the transactional session feature.
/// </summary>
public static class TransactionalSessionConfigurationExtensions
{
    /// <summary>
    /// Enables the transactional session for this endpoint.
    /// </summary>
    public static void EnableTransactionalSession(this PersistenceExtensions<CustomTestingPersistence> persistence,
        TransactionalSessionOptions transactionalSessionOptions = null)
    {
        var settings = persistence.GetSettings();

        settings.Set(transactionalSessionOptions ?? new TransactionalSessionOptions());
        settings.EnableFeatureByDefault<CustomTestingPersistenceTransactionalSession>();
    }
}