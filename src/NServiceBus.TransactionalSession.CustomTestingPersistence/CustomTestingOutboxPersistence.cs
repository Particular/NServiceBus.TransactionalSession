namespace NServiceBus.AcceptanceTesting;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Outbox;

sealed class CustomTestingOutboxPersistence : Feature
{
    public const string ProcessorEndpointKey = "CustomTestingPersistence.TransactionalSession.ProcessorEndpoint";

    public CustomTestingOutboxPersistence()
    {
        EnableByDefault<CustomTestingSynchronizedStorageFeature>();
        DependsOn<Outbox>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        if (context.Settings.TryGet<CustomTestingDatabase>(out var database))
        {
            context.Services.AddSingleton(database);
        }
        else
        {
            context.Services.AddSingleton<CustomTestingDatabase>();
        }

        var endpointName = context.Settings.GetOrDefault<string>(ProcessorEndpointKey) ?? context.Settings.EndpointName();

        context.Services.AddSingleton<IOutboxStorage>(sp => new CustomTestingOutboxStorage(sp.GetRequiredService<CustomTestingDatabase>(), endpointName));
    }
}