namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class TransactionSessionWithOutboxEndpoint : DefaultServer
    {
        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
            {
                await configurationBuilderCustomization(configuration);

                configuration.EnableTransactionalSession();
                configuration.EnableOutbox();

                configuration.RegisterStartupTask(provider => new CaptureServiceProviderStartupTask(provider, runDescriptor.ScenarioContext));
            });
    }
}