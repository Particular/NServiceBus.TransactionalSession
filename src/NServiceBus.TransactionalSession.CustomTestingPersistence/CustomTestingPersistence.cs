namespace NServiceBus.AcceptanceTesting;

using Persistence;

public class CustomTestingPersistence : PersistenceDefinition, IPersistenceDefinitionFactory<CustomTestingPersistence>
{
    CustomTestingPersistence() =>
        Supports<StorageType.Outbox, CustomTestingOutboxPersistence>();

    static CustomTestingPersistence IPersistenceDefinitionFactory<CustomTestingPersistence>.Create() => new();
}