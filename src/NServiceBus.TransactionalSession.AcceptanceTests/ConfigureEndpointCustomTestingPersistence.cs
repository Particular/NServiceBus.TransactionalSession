using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public class ConfigureEndpointCustomTestingPersistence : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        configuration.UsePersistence<CustomTestingPersistence>();
        return Task.CompletedTask;
    }

    public Task Cleanup()
    {
        // Nothing required for in-memory persistence
        return Task.CompletedTask;
    }
}