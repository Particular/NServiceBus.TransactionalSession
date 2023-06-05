namespace NServiceBus.TransactionalSession
{
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

            var informationHolder = new InformationHolderToAvoidClosures
            {
                LocalAddress = context.LocalQueueAddress(),
                IsOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox))
            };
            context.Services.AddSingleton(informationHolder);
            context.Services.AddScoped<PipelineIndicator>();
            context.Services.AddScoped(static sp =>
            {
                var informationHolder = sp.GetRequiredService<InformationHolderToAvoidClosures>();
                var pipelineIndicator = sp.GetRequiredService<PipelineIndicator>();
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(informationHolder.LocalAddress);

                ITransactionalSession transactionalSession;

                if (pipelineIndicator.WithinPipeline)
                {
                    transactionalSession = new PipelineAwareTransactionalSession(sp.GetServices<IOpenSessionOptionsCustomization>());
                }
                else if (informationHolder.IsOutboxEnabled)
                {
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

            context.Pipeline.Register(new PipelineIndicatorBehavior(), "Indicates that a pipeline is currently executing.");
            context.Pipeline.Register(new AttachInvokeHandlerContextBehavior(), "Attaches the handler context to the transactional session to auto open it.");

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
            public QueueAddress LocalAddress { get; set; }
            public bool IsOutboxEnabled { get; set; }
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public SessionCaptureTask(InformationHolderToAvoidClosures informationHolder) => this.informationHolder = informationHolder;

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                informationHolder.MessageSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            readonly InformationHolderToAvoidClosures informationHolder;
        }
    }
}