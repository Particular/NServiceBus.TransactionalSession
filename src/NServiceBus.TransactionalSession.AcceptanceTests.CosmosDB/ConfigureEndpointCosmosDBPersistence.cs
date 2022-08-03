using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.TransactionalSession.AcceptanceTests;

public class ConfigureEndpointCosmosDBPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<CosmosPersistence>();

        persistence.DisableContainerCreation();
        persistence.CosmosClient(SetupFixture.CosmosDbClient);
        persistence.DatabaseName(SetupFixture.DatabaseName);

        persistence.DefaultContainer(SetupFixture.ContainerName, SetupFixture.PartitionPathKey);

        return Task.CompletedTask;
    }

    public Task Cleanup() =>
        // Nothing required for in-memory persistence
        Task.CompletedTask;
}