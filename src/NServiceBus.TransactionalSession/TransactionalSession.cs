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
        //we need a processor address regardless of whether is the same endpoint that processes control message and business messages
        //so this is always assumed to be set
        if (!context.Settings.TryGet<TransactionalSessionOptions>(out var transactionalSessionOptions))
        {
            throw new InvalidOperationException("TransactionalSessionOptions is missing or not configured");
        }

        context.Services.AddTransient<SessionCaptureTask>();
        context.RegisterStartupTask(sp => sp.GetRequiredService<SessionCaptureTask>());

        var outboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));

        var informationHolder = new InformationHolderToAvoidClosures
        {
            //if the same endpoint processes the control message and business messages the processor address would be present and would be the address
            //If the above assumption is right may be we can change the LocalAddress to be something else?
            LocalAddress = outboxEnabled ? new QueueAddress(transactionalSessionOptions.ProcessorAddress) : null,
            IsOutboxEnabled = outboxEnabled
        };

        context.Services.AddSingleton(informationHolder);
        context.Services.AddSingleton(transactionalSessionOptions);
        context.Services.AddScoped(static sp =>
        {
            var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();
           ITransactionalSession transactionalSession;

            if (informationHolder.IsOutboxEnabled)
            {
                var physicalProcessorQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(informationHolder.LocalAddress);

                transactionalSession = new OutboxTransactionalSession(
                    sp.GetRequiredService<IOutboxStorage>(),
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    informationHolder.MessageSession,
                    sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetServices<IOpenSessionOptionsCustomization>(),
                    physicalProcessorQueueAddress);
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

        //we need to change this line of code so that the processor address is passed in when we have a processing endpoint
        context.Pipeline.Register(static sp =>
            new TransactionalSessionDelayControlMessageBehavior(
                sp.GetRequiredService<IMessageDispatcher>(),
                sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(sp.GetRequiredService<InformationHolderToAvoidClosures>().LocalAddress)
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