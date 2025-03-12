namespace NServiceBus.AcceptanceTesting;

using Features;
using Microsoft.Extensions.DependencyInjection;
using Persistence;

sealed class CustomTestingSynchronizedStorageFeature : Feature
{
    public CustomTestingSynchronizedStorageFeature() => DependsOn<SynchronizedStorage>();

    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Services.AddScoped<ICompletableSynchronizedStorageSession, CustomTestingSynchronizedStorageSession>();
    }
}