namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using NUnit.Framework;

[SetUpFixture]
public class SetupFixture
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
    }

    [OneTimeTearDown]
    public Task OneTimeTearDown()
    {
        return Table.DeleteIfExistsAsync();
    }

    public static string GetConnectionString() =>
        Environment.GetEnvironmentVariable("AzureTableServerConnectionString") ?? "UseDevelopmentStorage=true";

    public static string TableName;
    public static CloudTableClient TableClient;
    public static CloudTable Table;
}
