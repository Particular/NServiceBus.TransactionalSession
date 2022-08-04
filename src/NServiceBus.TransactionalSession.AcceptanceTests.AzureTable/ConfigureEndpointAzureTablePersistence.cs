using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.TransactionalSession.AcceptanceTests;

public class ConfigureEndpointAzureTablePersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<AzureTablePersistence>();

        persistence.ConnectionString(NServiceBus.TransactionalSession.AcceptanceTests.SetupFixture.GetConnectionString());
        persistence.DefaultTable(SetupFixture.TableName);

        return Task.CompletedTask;
    }

    public Task Cleanup() =>
        // Nothing required for in-memory persistence
        Task.CompletedTask;
}