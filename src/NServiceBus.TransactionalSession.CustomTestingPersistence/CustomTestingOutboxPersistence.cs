namespace NServiceBus.AcceptanceTesting
{
    using Features;
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

            context.Container.RegisterSingleton(typeof(IOutboxStorage), outboxStorage);
        }
    }
}