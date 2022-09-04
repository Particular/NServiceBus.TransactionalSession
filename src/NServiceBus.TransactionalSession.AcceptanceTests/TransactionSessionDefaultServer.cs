﻿namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class TransactionSessionDefaultServer : DefaultServer
    {
        public static Action<EndpointConfiguration> ConfigurePersistence { get; set; } =
            _ => throw new NotImplementedException();


        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization) =>
            base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
            {
                // Explicitly enforcing the type to be scanned. Otherwise the scanner would not pick it up in the acceptance tests
                endpointConfiguration.TypesToInclude.Add(typeof(TransactionalSession));
                endpointConfiguration.TypesToInclude.Add(typeof(CustomTestingPersistenceTransactionalSession));

                configuration.RegisterStartupTask(provider =>
                    new CaptureServiceProviderStartupTask(provider, runDescriptor.ScenarioContext));

                ConfigurePersistence(configuration);

                await configurationBuilderCustomization(configuration);
            });
    }
}