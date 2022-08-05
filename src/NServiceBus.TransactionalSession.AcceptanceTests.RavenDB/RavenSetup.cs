namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using NServiceBus;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;

    [SetUpFixture]
    public class RavenSetup
    {

        public const string DefaultDatabaseName = "TransactionalSessionTests";

        public static string TenantId { get; private set; }

        [OneTimeSetUp]
        public void Setup()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("Skip RavenDB tests on Windows");
            }

            var random = new Random();
            TenantId = "tenant-" + random.Next(1, 100);

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

                if (!result.Contains(TenantId))
                {
                    documentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(TenantId)));
                }

                persistence.SetDefaultDocumentStore(documentStore);
                persistence.SetMessageToDatabaseMappingConvention(headers =>
                {
                    if (headers.TryGetValue("tenant-id", out var tenantValue))
                    {
                        return tenantValue;
                    }

                    return DefaultDatabaseName;
                });
            };
        }
    }
}