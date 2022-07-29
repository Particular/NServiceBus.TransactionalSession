namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Support;

    public class TransactionSessionWithOutboxEndpoint : TransactionSessionWithoutOutboxEndpoint
    {
        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomizationConfiguration,
            Action<EndpointConfiguration> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointCustomizationConfiguration, configuration =>
            {
                configuration.EnableOutbox();

                configurationBuilderCustomization(configuration);
            });
    }
}