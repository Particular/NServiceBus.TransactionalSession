using System.Linq;
using NServiceBus;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

[SetUpFixture]
public class RavenSetup
{

    public const string DefaultDatabaseName = "TransactionalSessionTests";

    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<RavenDBPersistence>();
            var documentStore = new DocumentStore
            {
                Database = DefaultDatabaseName,
                Urls = new[] { "http://localhost:8080" }
            };
            documentStore.Initialize();
            var result = documentStore.Maintenance.Server.Send(new GetDatabaseNamesOperation(0, int.MaxValue));
            if (!result.Contains(DefaultDatabaseName))
            {
                documentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(DefaultDatabaseName)));
            }

            persistence.SetDefaultDocumentStore(documentStore);
        };
    }
}