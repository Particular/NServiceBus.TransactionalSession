namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using MongoDB.Driver;
    using NServiceBus;
    using NUnit.Framework;

    [SetUpFixture]
    public class MongoSetup
    {
        const string DatabaseName = "TransactionalSessionAcceptanceTests";

        MongoClient client;

        [OneTimeSetUp]
        public void Setup()
        {
            TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
            {
                var containerConnectionString = Environment.GetEnvironmentVariable("NServiceBusStorageMongoDB_ConnectionString");

                client = string.IsNullOrWhiteSpace(containerConnectionString) ? new MongoClient() : new MongoClient(containerConnectionString);

                var persistence = configuration.UsePersistence<MongoPersistence>();
                persistence.MongoClient(client);
                persistence.DatabaseName(DatabaseName);
                persistence.UseTransactions(true);
            };
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            try
            {
                await client.DropDatabaseAsync(DatabaseName);
            }
            catch (Exception e)
            {
                TestContext.WriteLine($"Error during MongoDB test cleanup: {e}");
            }
        }
    }
}