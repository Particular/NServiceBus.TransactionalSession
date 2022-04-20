namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;
    using Persistence;
    using Pipeline;
    using Transport;

    class UnitOfWorkDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            throw new NotImplementedException();
        }
    }

    class UnitOfWorkControlMessageExceptionBehavior : Behavior<ITransportReceiveContext>
    {
        public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            throw new NotImplementedException();
        }
    }

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            bool isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped<ITransactionalSession>(sp =>
            {
                if (isOutboxEnabled)
                {
                    return new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress));
                }
                else
                {
                    return new TransactionalSession(
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>());
                }
            });
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