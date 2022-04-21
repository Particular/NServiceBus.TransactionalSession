namespace NServiceBus.TransactionalSession.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;

    class DefaultEndpoint : IEndpointSetupTemplate
    {
#pragma warning disable PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
        public async Task<EndpointConfiguration> GetConfiguration(
#pragma warning restore PS0018 // A task-returning method should have a CancellationToken parameter unless it has a parameter implementing ICancellableContext
            RunDescriptor runDescriptor,
            EndpointCustomizationConfiguration endpointConfiguration,
#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
            Func<EndpointConfiguration, Task> configurationBuilderCustomization)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
        {
            var configuration = new EndpointConfiguration(endpointConfiguration.EndpointName);

            configuration.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());
            configuration.EnableInstallers();

            configuration.RegisterComponents(c => c
                .AddSingleton(runDescriptor.ScenarioContext.GetType(), runDescriptor.ScenarioContext));

            var recoverability = configuration.Recoverability();
            recoverability.Delayed(delayed => delayed.NumberOfRetries(0));
            recoverability.Immediate(immediate => immediate.NumberOfRetries(0));
            configuration.SendFailedMessagesTo("error");

            configuration.UseTransport(new LearningTransport
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly
            });

            configuration.UsePersistence<AcceptanceTestingPersistence>();

            await configurationBuilderCustomization(configuration).ConfigureAwait(false);

            return configuration;
        }
    }
}