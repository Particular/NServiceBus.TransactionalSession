namespace NServiceBus.AcceptanceTesting;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Outbox;

sealed class CustomTestingOutboxPersistence : Feature
{
    public const string OwningEndpointNameKey = "CustomTestingPersistence.Outbox.OwningEndpoint";

    public CustomTestingOutboxPersistence()
    {
        DependsOn<Outbox>();
        Defaults(s => s.EnableFeatureByDefault<CustomTestingSynchronizedStorageFeature>());
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

        var endpointName = context.Settings.GetOrDefault<string>(OwningEndpointNameKey) ?? context.Settings.EndpointName();

        context.Services.AddSingleton<IOutboxStorage>(sp => new CustomTestingOutboxStorage(sp.GetRequiredService<CustomTestingDatabase>(), endpointName));
    }
}