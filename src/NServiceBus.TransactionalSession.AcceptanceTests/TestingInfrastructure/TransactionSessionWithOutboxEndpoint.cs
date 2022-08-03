namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Support;

    public class TransactionSessionWithOutboxEndpoint : TransactionSessionDefaultServer
    {
        public override Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            return base.GetConfiguration(runDescriptor, endpointConfiguration, async configuration =>
            {
                await configurationBuilderCustomization(configuration);

                configuration.EnableOutbox();
            });
        }
    }
}