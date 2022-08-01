using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

public class ConfigureEndpointSqlPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<SqlPersistence>();
        persistence.ConnectionBuilder(() =>
        {
            var environmentVariableName = "SQLServerConnectionString";
            var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

            if (connectionString == null)
            {
                throw new Exception($"No connection string found in environment variable {environmentVariableName}");
            }

            return new SqlConnection(connectionString);
        });

        persistence.SqlDialect<SqlDialect.MsSqlServer>();

        return Task.CompletedTask;
    }

    public Task Cleanup() =>
        // Nothing required for in-memory persistence
        Task.CompletedTask;
}