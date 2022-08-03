using NServiceBus;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NUnit.Framework;

[SetUpFixture]
public class MongoSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<MongoPersistence>();
            persistence.DatabaseName("TransactionalSessionAcceptanceTests");

            //TODO: use transactions?
        };
    }
}