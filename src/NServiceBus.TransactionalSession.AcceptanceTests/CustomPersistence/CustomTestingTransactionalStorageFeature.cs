namespace NServiceBus.AcceptanceTesting
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;

    class CustomTestingTransactionalStorageFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddScoped<ICompletableSynchronizedStorageSession, CustomTestingSynchronizedStorageSession>();
        }
    }
}