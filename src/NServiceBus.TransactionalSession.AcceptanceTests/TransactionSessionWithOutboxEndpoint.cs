namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.TransactionalSession;

    public class TransactionSessionWithOutboxEndpoint : DefaultServer
    {
        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration,
            Action<EndpointConfiguration> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointCustomizationConfiguration, configuration =>
            {
                configurationBuilderCustomization(configuration);

                configuration.EnableTransactionalSession();
                configuration.EnableOutbox();

                //configuration.RegisterStartupTask(provider => new CaptureServiceProviderStartupTask(provider, runDescriptor.ScenarioContext));
            });
    }
}