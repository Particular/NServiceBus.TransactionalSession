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
        var outboxStorage = new CustomTestingOutboxStorage();

        context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);
    }
}