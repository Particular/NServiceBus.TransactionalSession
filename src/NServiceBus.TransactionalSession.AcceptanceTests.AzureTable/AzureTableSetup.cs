namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using NUnit.Framework;

[SetUpFixture]
[EnvironmentSpecificTest(EnvironmentVariables.AzureTableServerConnectionString)]
public class AzureTableSetup
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var connectionString = GetConnectionString();

        TableName = $"{Path.GetFileNameWithoutExtension(Path.GetTempFileName())}{DateTime.UtcNow.Ticks}"
            .ToLowerInvariant();

        var account = CloudStorageAccount.Parse(connectionString);
        TableClient = account.CreateCloudTableClient();
        Table = TableClient.GetTableReference(TableName);
        await Table.CreateIfNotExistsAsync();

        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<AzureTablePersistence>();

            persistence.ConnectionString(GetConnectionString());
            persistence.DefaultTable(TableName);
        };
    }

    [OneTimeTearDown]
    public Task OneTimeTearDown() => Table.DeleteIfExistsAsync();

    public static string GetConnectionString() =>
        Environment.GetEnvironmentVariable(EnvironmentVariables.AzureTableServerConnectionString) ?? "UseDevelopmentStorage=true";

    public static string TableName;
    public static CloudTableClient TableClient;
    public static CloudTable Table;
}
