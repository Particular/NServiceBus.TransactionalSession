namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;
    using Persistence;
    using Transport;

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            bool isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped<ITransactionalSession, TransactionalSession>(sp =>
                new TransactionalSession(
                    sp.GetRequiredService<IOutboxStorage>(),
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    sessionCaptureTask.CapturedSession,
                    sp.GetRequiredService<IMessageDispatcher>(),
                    isOutboxEnabled,
                    sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress)));
        }

        public class SessionCaptureTask : FeatureStartupTask
        {
            public IMessageSession CapturedSession { get; set; }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                CapturedSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        }
    }
}