namespace NServiceBus.TransactionalSession;

using System;
using AcceptanceTesting;
using Configuration.AdvancedExtensibility;

public static class CustomTestingPersistenceConfigExtensions
{
    public static PersistenceExtensions<CustomTestingPersistence> UseDatabase(this PersistenceExtensions<CustomTestingPersistence> persistenceExtensions, CustomTestingDatabase database)
    {
        ArgumentNullException.ThrowIfNull(persistenceExtensions);
        ArgumentNullException.ThrowIfNull(database);

        persistenceExtensions.GetSettings().Set(database);
        return persistenceExtensions;
    }
}