namespace NServiceBus.TransactionalSession;

using System;
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
    /// Enables the transactional session for this endpoint using the specified TransactionalSessionOptions.
    /// </summary>
    public static void EnableTransactionalSession(this PersistenceExtensions<CustomTestingPersistence> persistence,
        TransactionalSessionOptions transactionalSessionOptions)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(transactionalSessionOptions);

        var settings = persistence.GetSettings();

        settings.Set(transactionalSessionOptions);
        settings.EnableFeatureByDefault<CustomTestingPersistenceTransactionalSession>();
    }
}