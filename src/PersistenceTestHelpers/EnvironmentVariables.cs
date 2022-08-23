using System.Collections.Generic;

public static class EnvironmentVariables
{
    public const string AzureTableServerConnectionString = "AzureTableServerConnectionString";

    public const string RavenDBConnectionString = "RavenDBConnectionString";

    public const string SqlServerConnectionString = "SqlServerConnectionString";

    public const string CosmosConnectionString = "CosmosDBPersistence_ConnectionString";

    public const string MongoDBConnectionString = "NServiceBusStorageMongoDB_ConnectionString";

    public static IReadOnlyList<string> Names { get; } = new List<string>
    {
        AzureTableServerConnectionString,
        RavenDBConnectionString,
        CosmosConnectionString,
        MongoDBConnectionString,
        SqlServerConnectionString,
    };
}