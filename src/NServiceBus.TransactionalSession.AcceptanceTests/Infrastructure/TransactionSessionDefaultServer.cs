namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Infrastructure;

    public class TransactionSessionDefaultServer : DefaultServer
    {
        public override async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Action<EndpointConfiguration> configurationBuilderCustomization) =>
            await base.GetConfiguration(runDescriptor, endpointConfiguration, configuration =>
        {
            var persistence = configuration.UsePersistence<CustomTestingPersistence>();
            persistence.EnableTransactionalSession();

            configuration.EnableFeature<CaptureBuilderFeature>();

            configurationBuilderCustomization(configuration);
        });
    }
}