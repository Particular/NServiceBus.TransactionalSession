namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using MongoDB.Driver;
    using NServiceBus;
    using NUnit.Framework;

    [SetUpFixture]
    [EnvironmentSpecificTest(EnvironmentVariables.MongoDBConnectionString)]
    public class MongoSetup
    {
        public const string DatabaseName = "TransactionalSessionAcceptanceTests";

        public static MongoClient MongoClient { get; private set; }

        [OneTimeSetUp]
        public void Setup()
        {
            var containerConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.MongoDBConnectionString);
            MongoClient = string.IsNullOrWhiteSpace(containerConnectionString) ? new MongoClient() : new MongoClient(containerConnectionString);

            TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
            {
                var persistence = configuration.UsePersistence<MongoPersistence>();
                persistence.MongoClient(MongoClient);
                persistence.DatabaseName(DatabaseName);
                persistence.UseTransactions(true);
            };
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            try
            {
                await MongoClient.DropDatabaseAsync(DatabaseName);
            }
            catch (Exception e)
            {
                TestContext.WriteLine($"Error during MongoDB test cleanup: {e}");
            }
        }
    }
}