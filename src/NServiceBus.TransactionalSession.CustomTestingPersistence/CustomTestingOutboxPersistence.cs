namespace NServiceBus.AcceptanceTesting
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;

    class CustomTestingOutboxPersistence : Feature
    {
        public CustomTestingOutboxPersistence()
        {
            DependsOn<Outbox>();
            Defaults(s => s.EnableFeatureByDefault(typeof(CustomTestingTransactionalStorageFeature)));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var outboxStorage = new CustomTestingOutboxStorage();

            context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);
        }
    }
}