namespace NServiceBus.AcceptanceTesting;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Outbox;

sealed class CustomTestingOutboxPersistence : Feature
{
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

        context.Services.AddSingleton<IOutboxStorage>(sp => new CustomTestingOutboxStorage(sp.GetRequiredService<CustomTestingDatabase>(), context.Settings.EndpointName()));
    }
}