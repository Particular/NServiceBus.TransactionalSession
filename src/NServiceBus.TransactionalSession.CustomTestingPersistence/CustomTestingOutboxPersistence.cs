namespace NServiceBus.AcceptanceTesting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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

            context.RegisterStartupTask(new OutboxCleaner(outboxStorage, TimeSpan.FromDays(5)));
        }

        class OutboxCleaner : FeatureStartupTask
        {
            public OutboxCleaner(CustomTestingOutboxStorage storage, TimeSpan timeToKeepDeduplicationData)
            {
                this.timeToKeepDeduplicationData = timeToKeepDeduplicationData;
                customTestingOutboxStorage = storage;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
            {
                using (var waitHandle = new ManualResetEvent(false))
                {
                    cleanupTimer.Dispose(waitHandle);

                    // TODO: Use async synchronization primitive
                    waitHandle.WaitOne();
                }
                return Task.CompletedTask;
            }

            void PerformCleanup(object state)
            {
                customTestingOutboxStorage.RemoveEntriesOlderThan(DateTime.UtcNow - timeToKeepDeduplicationData);
            }

            readonly CustomTestingOutboxStorage customTestingOutboxStorage;
            readonly TimeSpan timeToKeepDeduplicationData;

            Timer cleanupTimer;
        }
    }
}