using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NUnit.Framework;

[SetUpFixture]
public class TestingPersistenceSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<CustomTestingPersistence>();
        };
    }
}