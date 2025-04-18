﻿namespace NServiceBus.TransactionalSession;

using System.Threading;
using System.Threading.Tasks;
using Features;
using Microsoft.Extensions.DependencyInjection;
using Outbox;
using Persistence;
using Transport;

/// <summary>
/// Provides <see cref="ITransactionalSession" /> integration feature.
/// </summary>
public abstract class TransactionalSession : Feature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionalSession"/> feature.
    /// </summary>
    protected TransactionalSession()
    {
        DependsOn<SynchronizedStorage>();
        DependsOnOptionally<Outbox>();
    }

    /// <summary>
    /// See <see cref="Feature.Setup" />.
    /// </summary>
    protected override void Setup(FeatureConfigurationContext context)
    {
        context.Services.AddTransient<SessionCaptureTask>();
        context.RegisterStartupTask(sp => sp.GetRequiredService<SessionCaptureTask>());

        var outboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));

        var informationHolder = new InformationHolderToAvoidClosures
        {
            LocalAddress = outboxEnabled ? context.LocalQueueAddress() : null,
            IsOutboxEnabled = outboxEnabled
        };

        context.Services.AddSingleton(informationHolder);
        context.Services.AddScoped(static sp =>
        {
            var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();

            ITransactionalSession transactionalSession;

            if (informationHolder.IsOutboxEnabled)
            {
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(informationHolder.LocalAddress);

                transactionalSession = new OutboxTransactionalSession(
                    sp.GetRequiredService<IOutboxStorage>(),
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    informationHolder.MessageSession,
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetServices<IOpenSessionOptionsCustomization>(),
                    physicalLocalQueueAddress);
            }
            else
            {
                transactionalSession = new NonOutboxTransactionalSession(
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    informationHolder.MessageSession,
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetServices<IOpenSessionOptionsCustomization>());
            }

            return transactionalSession;
        });

        if (!informationHolder.IsOutboxEnabled)
        {
            return;
        }

        context.Pipeline.Register(static sp =>
            new TransactionalSessionDelayControlMessageBehavior(
                sp.GetRequiredService<IMessageDispatcher>(),
                sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(sp.GetRequiredService<InformationHolderToAvoidClosures>().LocalAddress)
            ), "Transaction commit control message delay behavior");

        context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
            "Transaction commit control message delay acknowledgement behavior");
    }

    // This class is a bit of a weird mix of things that are set upfront and things that are set
    // when the dependencies are around. Not ideal but it helps to avoid closures
    sealed class InformationHolderToAvoidClosures
    {
        public IMessageSession MessageSession { get; set; }
        public QueueAddress LocalAddress { get; init; }
        public bool IsOutboxEnabled { get; init; }
    }

    class SessionCaptureTask(InformationHolderToAvoidClosures informationHolder) : FeatureStartupTask
    {
        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            informationHolder.MessageSession = session;
            return Task.CompletedTask;
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}