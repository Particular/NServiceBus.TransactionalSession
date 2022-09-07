namespace NServiceBus.AcceptanceTesting
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;

    class SynchronizedStorage : Feature
    {
        public SynchronizedStorage()
        {
            DependsOn<Features.SynchronizedStorage>();
        }
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddScoped<ICompletableSynchronizedStorageSession, StorageSession>();
        }
    }
}