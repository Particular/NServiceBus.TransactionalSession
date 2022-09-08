namespace NServiceBus.TransactionalSession
{
    using System.Threading.Tasks;
    using Features;
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
            var informationHolder = new InformationHolderToAvoidClosures
            {
                LocalAddress = context.Settings.LocalAddress(),
                IsOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox))
            };
            context.RegisterStartupTask(b => new SessionCaptureTask(informationHolder));

            context.Container.ConfigureComponent(static sp =>
            {
                var informationHolder = sp.Build<InformationHolderToAvoidClosures>();

                ITransactionalSession transactionalSession;

                if (informationHolder.IsOutboxEnabled)
                {
                    transactionalSession = new OutboxTransactionalSession(
                        sp.Build<IOutboxStorage>(),
                        sp.Build<CompletableSynchronizedStorageSessionAdapter>(),
                        informationHolder.MessageSession,
                        sp.Build<IDispatchMessages>(),
                        sp.BuildAll<IOpenSessionOptionsCustomization>(),
                        informationHolder.LocalAddress);
                }
                else
                {
                    transactionalSession = new NonOutboxTransactionalSession(
                        sp.Build<CompletableSynchronizedStorageSessionAdapter>(),
                        informationHolder.MessageSession,
                        sp.Build<IDispatchMessages>(),
                        sp.BuildAll<IOpenSessionOptionsCustomization>());
                }

                return transactionalSession;
            }, DependencyLifecycle.InstancePerUnitOfWork);

            if (!informationHolder.IsOutboxEnabled)
            {
                return;
            }

            context.Pipeline.Register(static sp =>
                    new TransactionalSessionDelayControlMessageBehavior(
                        sp.Build<IDispatchMessages>(),
                        sp.Build<InformationHolderToAvoidClosures>().LocalAddress),
                "Transaction commit control message delay behavior");

            context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
                "Transaction commit control message delay acknowledgement behavior");
        }

        // This class is a bit of a weird mix of things that are set upfront and things that are set
        // when the dependencies are around. Not ideal but it helps to avoid closures
        sealed class InformationHolderToAvoidClosures
        {
            public IMessageSession MessageSession { get; set; }
            public string LocalAddress { get; set; }
            public bool IsOutboxEnabled { get; set; }
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public SessionCaptureTask(InformationHolderToAvoidClosures informationHolder) => this.informationHolder = informationHolder;

            protected override Task OnStart(IMessageSession session)
            {
                informationHolder.MessageSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session) =>
                Task.CompletedTask;

            readonly InformationHolderToAvoidClosures informationHolder;
        }
    }
}