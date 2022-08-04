namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public class TransactionSessionDefaultServer : DefaultServer
{
    public static Action<EndpointConfiguration> ConfigurePersistence { get; set; } =
        _ => throw new NotImplementedException();


    public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
        base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
        {
            configuration.EnableTransactionalSession();
            configuration.RegisterStartupTask(provider =>
                new CaptureServiceProviderStartupTask(provider, runDescriptor.ScenarioContext));

            ConfigurePersistence(configuration);

            await configurationBuilderCustomization(configuration);
        });
}