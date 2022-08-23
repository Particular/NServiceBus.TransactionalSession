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
    [ExecuteOnlyForEnvironmentWith(EnvironmentVariables.RavenDBConnectionString)]
    public class RavenSetup
    {
        public const string DefaultDatabaseName = "TransactionalSessionTests";

        public static string TenantId { get; private set; }
        public static DocumentStore DocumentStore { get; private set; }

        [OneTimeSetUp]
        public void Setup()
        {
#if RELEASE
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("Skip RavenDB tests on Windows");
            }
#endif

            var random = new Random();
            TenantId = "tenant-" + random.Next(1, 100);

            DocumentStore = new DocumentStore
            {
                Database = DefaultDatabaseName,
                Urls = new[] { GetConnectionString() }
            };
            DocumentStore.Initialize();
            var result = DocumentStore.Maintenance.Server.Send(new GetDatabaseNamesOperation(0, int.MaxValue));

            if (!result.Contains(DefaultDatabaseName))
            {
                DocumentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(DefaultDatabaseName)));
            }

            if (!result.Contains(TenantId))
            {
                DocumentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(TenantId)));
            }

            TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
            {
                var persistence = configuration.UsePersistence<RavenDBPersistence>();
                persistence.SetDefaultDocumentStore(DocumentStore);
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

        static string GetConnectionString()
        {
            var connectionString = EnvironmentHelper.GetEnvironmentVariable(EnvironmentVariables.RavenDBConnectionString);

            if (connectionString == null)
            {
                throw new Exception($"No connection string found in environment variable {EnvironmentVariables.RavenDBConnectionString}");
            }

            return connectionString;
        }
    }
}