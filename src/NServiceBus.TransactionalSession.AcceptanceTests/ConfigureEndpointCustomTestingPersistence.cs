using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.TransactionalSession;

public class ConfigureEndpointCustomTestingPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var persistence = configuration.UsePersistence<CustomTestingPersistence>();
        persistence.EnableTransactionalSession();

        return Task.CompletedTask;
    }

    public Task Cleanup() => Task.CompletedTask;
}