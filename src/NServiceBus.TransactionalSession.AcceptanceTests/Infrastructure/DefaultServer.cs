namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.IO;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using NUnit.Framework;

public class DefaultServer : IEndpointSetupTemplate
{
    public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointCustomization,
        Func<EndpointConfiguration, Task> configurationBuilderCustomization)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointCustomization.EndpointName);

        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.Recoverability()
            .Delayed(delayed => delayed.NumberOfRetries(0))
            .Immediate(immediate => immediate.NumberOfRetries(0));
        endpointConfiguration.SendFailedMessagesTo("error");

        var storageDir = Path.Combine(Path.GetTempPath(), "learn", TestContext.CurrentContext.Test.ID);

        endpointConfiguration.UseTransport(new AcceptanceTestingTransport { StorageLocation = storageDir });
        endpointConfiguration.PurgeOnStartup(true);

        var persistence = endpointConfiguration.UsePersistence<CustomTestingPersistence>();

        if (runDescriptor.ScenarioContext is TransactionalSessionTestContext testContext)
        {
            endpointConfiguration.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, testContext, endpointCustomization.EndpointName));

            persistence.UseDatabase(testContext.Database);
        }

        endpointConfiguration.GetSettings().Set(persistence);

        await configurationBuilderCustomization(endpointConfiguration);

        endpointConfiguration.ScanTypesForTest(endpointCustomization);

        return endpointConfiguration;
    }
}