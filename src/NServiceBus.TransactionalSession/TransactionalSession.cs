namespace NServiceBus.TransactionalSession;

using System;
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
        Defaults(s => s.Set("Outbox.AllowUseWithoutReceiving", true));
        DependsOn<SynchronizedStorage>();
        DependsOnOptionally<Outbox>();
    }

    /// <summary>
    /// See <see cref="Feature.Setup" />.
    /// </summary>
    protected override void Setup(FeatureConfigurationContext context)
    {
        if (!context.Settings.TryGet<TransactionalSessionOptions>(out var transactionalSessionOptions))
        {
            throw new InvalidOperationException("TransactionalSessionOptions is missing or not configured");
        }

        context.Services.AddTransient<SessionCaptureTask>();
        context.RegisterStartupTask(sp => sp.GetRequiredService<SessionCaptureTask>());

        var informationHolder = new InformationHolderToAvoidClosures
        {
            IsOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox)),
            ProcessorAddress = GetProcessorAddress(context, transactionalSessionOptions)
        };

        context.Services.AddSingleton(informationHolder);
        context.Services.AddScoped(static sp =>
        {
            var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();

            ITransactionalSession transactionalSession;

            if (informationHolder.IsOutboxEnabled)
            {
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(informationHolder.ProcessorAddress);

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
                sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(sp.GetRequiredService<InformationHolderToAvoidClosures>().ProcessorAddress)
            ), "Transaction commit control message delay behavior");

        context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
            "Transaction commit control message delay acknowledgement behavior");
    }

    static QueueAddress GetProcessorAddress(FeatureConfigurationContext context, TransactionalSessionOptions transactionalSessionOptions)
    {
        var settings = context.Settings;

        if (!settings.IsFeatureActive(typeof(Outbox)))
        {
            return null;
        }

        if (!settings.GetOrDefault<bool>("Endpoint.SendOnly"))
        {
            return context.LocalQueueAddress();
        }

        if (string.IsNullOrEmpty(transactionalSessionOptions.ProcessorAddress))
        {
            throw new InvalidOperationException("Send only endpoints needs to have a processor endpoint configured");
        }

        return new QueueAddress(transactionalSessionOptions.ProcessorAddress);
    }

    // This class is a bit of a weird mix of things that are set upfront and things that are set
    // when the dependencies are around. Not ideal but it helps to avoid closures
    sealed class InformationHolderToAvoidClosures
    {
        public IMessageSession MessageSession { get; set; }
        public QueueAddress ProcessorAddress { get; init; }
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