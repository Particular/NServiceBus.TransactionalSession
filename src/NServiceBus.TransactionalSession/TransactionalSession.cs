namespace NServiceBus.TransactionalSession;

using System;
using System.Diagnostics.Metrics;
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
        //having this turned on does not cause any harm, so we can leave it on even if the TS does not use Outbox
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

        var outboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
        var isSendOnly = context.Settings.GetOrDefault<bool>("Endpoint.SendOnly");
        QueueAddress? processorAddress = null;

        if (outboxEnabled)
        {
            if (isSendOnly && string.IsNullOrWhiteSpace(transactionalSessionOptions.ProcessorEndpoint))
            {
                throw new InvalidOperationException(
                    "A configured ProcessorEndpoint is required when using the transactional session and the outbox with send-only endpoints");
            }

            if (!string.IsNullOrEmpty(transactionalSessionOptions.ProcessorEndpoint) && !isSendOnly)

            {
                throw new InvalidOperationException(
                    "A ProcessorEndpoint can only be specified for send-only endpoints");
            }

            processorAddress = string.IsNullOrWhiteSpace(transactionalSessionOptions.ProcessorEndpoint)
                ? context.LocalQueueAddress()
                : new QueueAddress(transactionalSessionOptions.ProcessorEndpoint);
        }

        if (!outboxEnabled && !string.IsNullOrEmpty(transactionalSessionOptions.ProcessorEndpoint))
        {
            throw new InvalidOperationException(
                "A ProcessorEndpoint can only be specified for send-only endpoints with Outbox enabled");
        }

        var informationHolder = new InformationHolderToAvoidClosures
        {
            IsOutboxEnabled = outboxEnabled,
            ControlMessageProcessorAddress = processorAddress,
            IsSendOnly = isSendOnly,
            EndpointName = context.Settings.EndpointName()
        };

        context.Services.AddSingleton(static sp =>
        {
            var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();

            var transactionalSessionMetrics = new TransactionalSessionMetrics(
                sp.GetRequiredService<IMeterFactory>(),
                informationHolder.EndpointName!
            );

            return transactionalSessionMetrics;
        });

        context.Services.AddSingleton(informationHolder);
        context.Services.AddSingleton(transactionalSessionOptions);
        context.Services.AddSingleton(informationHolder);
        context.Services.AddScoped(static sp =>
        {
            var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();
            ITransactionalSession transactionalSession;

            if (informationHolder.IsOutboxEnabled)
            {
                var physicalProcessorQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(informationHolder.ControlMessageProcessorAddress);

                transactionalSession = new OutboxTransactionalSession(
                    sp.GetRequiredService<IOutboxStorage>(),
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    informationHolder.MessageSession ?? throw new InvalidOperationException("Message session is not available"),
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetServices<IOpenSessionOptionsCustomization>(),
                    physicalProcessorQueueAddress,
                    informationHolder.IsSendOnly,
                    sp.GetRequiredService<TransactionalSessionMetrics>());
            }
            else
            {
                transactionalSession = new NonOutboxTransactionalSession(
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    informationHolder.MessageSession ?? throw new InvalidOperationException("Message session is not available"),
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetServices<IOpenSessionOptionsCustomization>(),
                    sp.GetRequiredService<TransactionalSessionMetrics>());
            }

            return transactionalSession;
        });

        context.Settings.AddStartupDiagnosticsSection("NServiceBus.TransactionalSession",
            new
            {
                UsingOutbox = outboxEnabled,
                ProcessorEndpointAddress = informationHolder.ControlMessageProcessorAddress
            });

        if (!informationHolder.IsOutboxEnabled)
        {
            return;
        }

        context.Pipeline.Register(static sp =>
            new TransactionalSessionDelayControlMessageBehavior(
                sp.GetRequiredService<IMessageDispatcher>(),
                sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(sp.GetRequiredService<InformationHolderToAvoidClosures>()
                        .ControlMessageProcessorAddress),
                sp.GetRequiredService<TransactionalSessionMetrics>()
            ), "Transaction commit control message delay behavior");

        context.Pipeline.Register(static sp =>
                new TransactionalSessionControlMessageExceptionBehavior(sp.GetRequiredService<TransactionalSessionMetrics>()),
            "Transaction commit control message delay acknowledgement behavior");
    }

    // This class is a bit of a weird mix of things that are set upfront and things that are set
    // when the dependencies are around. Not ideal but it helps to avoid closures
    sealed class InformationHolderToAvoidClosures
    {
        public IMessageSession? MessageSession { get; set; }
        public QueueAddress? ControlMessageProcessorAddress { get; init; }
        public bool IsOutboxEnabled { get; init; }
        public bool IsSendOnly { get; init; }
        public string? EndpointName { get; set; }
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