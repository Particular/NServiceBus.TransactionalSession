using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Persistence;

public class ConfigureEndpointNHibernatePersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<NHibernatePersistence>();

        var environmentVariableName = "SQLServerConnectionString";
        var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

        if (connectionString == null)
        {
            throw new Exception($"No connection string found in environment variable {environmentVariableName}");
        }

        persistence.ConnectionString(connectionString);

        return Task.CompletedTask;
    }

    public Task Cleanup() =>
        // Nothing required for in-memory persistence
        Task.CompletedTask;
}