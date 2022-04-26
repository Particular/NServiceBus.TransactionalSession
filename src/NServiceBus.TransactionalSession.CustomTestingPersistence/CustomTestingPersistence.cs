namespace NServiceBus.AcceptanceTesting
{
    using Features;
    using Persistence;

    public class CustomTestingPersistence : PersistenceDefinition
    {
        internal CustomTestingPersistence()
        {
            Supports<StorageType.Outbox>(s =>
            {
                s.EnableFeatureByDefault<CustomTestingOutboxPersistence>();
                s.EnableFeatureByDefault<CustomTestingTransactionalStorageFeature>();
            });
        }
    }
}