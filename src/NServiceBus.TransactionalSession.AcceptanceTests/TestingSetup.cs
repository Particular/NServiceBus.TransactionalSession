namespace NServiceBus.TransactionalSession.AcceptanceTests;

using AcceptanceTesting;
using NUnit.Framework;

[SetUpFixture]
public class TestingSetup
{

    [OneTimeSetUp]
    public void Setup()
    {
        typeof(ITransactionalSession).ToString();
        typeof(CustomTestingPersistence).ToString();

        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<CustomTestingPersistence>();
        };
    }
}