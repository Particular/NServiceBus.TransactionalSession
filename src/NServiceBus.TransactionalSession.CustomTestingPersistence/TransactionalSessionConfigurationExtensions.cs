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
    public static void EnableTransactionalSession(this PersistenceExtensions<CustomTestingPersistence> persistence) => EnableTransactionalSession(persistence, new TransactionalSessionOptions());

    /// <summary>
    /// Enables the transactional session for this endpoint.
    /// </summary>
    public static void EnableTransactionalSession(this PersistenceExtensions<CustomTestingPersistence> persistence,
        TransactionalSessionOptions transactionalSessionOptions)
    {
        var settings = persistence.GetSettings();

        settings.Set(transactionalSessionOptions ?? new TransactionalSessionOptions());
        settings.EnableFeatureByDefault<CustomTestingPersistenceTransactionalSession>();
    }
}