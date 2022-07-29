﻿namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;
    using Persistence;
    using Transport;

    /// <summary>
    /// Provides <see cref="ITransactionalSession" />.
    /// </summary>
    public class TransactionalSessionFeature : Feature
    {
        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            if (context.Settings.TryGet("NServiceBus.Persistence.CosmosDB.OutboxStorage", out FeatureState state) && state == FeatureState.Active)
            {
                context.Pipeline.Register(new CosmosControlMessageBehavior(), "TODO");
            }

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped<ITransactionalSession>(sp =>
            {
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(localQueueAddress);

                if (isOutboxEnabled)
                {
                    return new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        physicalLocalQueueAddress
                        );
                }

                return new TransactionalSession(
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    sessionCaptureTask.CapturedSession,
                    sp.GetRequiredService<IMessageDispatcher>());
            });

            if (isOutboxEnabled)
            {
                context.Pipeline.Register(sp => new TransactionalSessionDelayControlMessageBehavior(sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress)
                ), "Transaction commit control message delay behavior");

                context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
                    "Transaction commit control message delay acknowledgement behavior");
            }
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public IMessageSession CapturedSession { get; set; }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                CapturedSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        }
    }
}