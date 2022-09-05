namespace NServiceBus.AcceptanceTesting
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;

    class OutboxStorage : Feature
    {
        public OutboxStorage()
        {
            DependsOn<Outbox>();
            Defaults(s => s.EnableFeatureByDefault<SynchronizedStorage>());
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var outboxStorage = new OutboxPersister();

            context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);
        }
    }
}