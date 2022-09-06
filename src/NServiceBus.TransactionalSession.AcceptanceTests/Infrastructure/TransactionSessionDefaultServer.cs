namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;

    public class TransactionSessionDefaultServer : DefaultServer
    {
        public override async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            await base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
        {
            var persistence = configuration.UsePersistence<CustomTestingPersistence>();
            persistence.EnableTransactionalSession();

            configuration.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, runDescriptor.ScenarioContext));

            await configurationBuilderCustomization(configuration);
        });
    }
}