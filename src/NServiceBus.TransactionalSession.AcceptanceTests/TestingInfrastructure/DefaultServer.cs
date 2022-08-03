﻿namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTesting.Support;

public class DefaultServer : IEndpointSetupTemplate
{
    protected TransportTransactionMode TransportTransactionMode { get; set; } = TransportTransactionMode.SendsAtomicWithReceive;

    public virtual async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration,
#pragma warning disable PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
        Func<EndpointConfiguration, Task> configurationBuilderCustomization)
#pragma warning restore PS0013 // A Func used as a method parameter with a Task, ValueTask, or ValueTask<T> return type argument should have at least one CancellationToken parameter type argument unless it has a parameter type argument implementing ICancellableContext
    {
        var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
        builder.EnableInstallers();

        builder.Recoverability()
            .Delayed(delayed => delayed.NumberOfRetries(0))
            .Immediate(immediate => immediate.NumberOfRetries(0));
        builder.SendFailedMessagesTo("error");

        builder.UseTransport(new AcceptanceTestingTransport { TransportTransactionMode = TransportTransactionMode });

        // TODO: won't be necessary with NSB.AcceptanceTesting > beta5!
        builder.RegisterComponents(r => { RegisterInheritanceHierarchyOfContextOnContainer(runDescriptor, r); });

        await configurationBuilderCustomization(builder).ConfigureAwait(false);

        // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
        builder.TypesToIncludeInScan(endpointConfiguration.GetTypesScopedByTestClass());

        return builder;
    }

    static void RegisterInheritanceHierarchyOfContextOnContainer(RunDescriptor runDescriptor, IServiceCollection r)
    {
        var type = runDescriptor.ScenarioContext.GetType();
        while (type != typeof(object))
        {
            r.AddSingleton(type, runDescriptor.ScenarioContext);
            type = type.BaseType;
        }
    }
}