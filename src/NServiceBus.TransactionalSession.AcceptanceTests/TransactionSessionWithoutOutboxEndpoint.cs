namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.EndpointTemplates;

public class TransactionSessionWithoutOutboxEndpoint : DefaultServer
{
    public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration,
        Action<EndpointConfiguration> configurationBuilderCustomization)
    {
        endpointCustomizationConfiguration.TypesToInclude.Add(typeof(CaptureBuilderFeature));

        return base.GetConfiguration(runDescriptor, endpointCustomizationConfiguration, configuration =>
        {
            configuration.EnableTransactionalSession();

            configuration.EnableFeature<CaptureBuilderFeature>();

            configurationBuilderCustomization(configuration);
        });
    }
}