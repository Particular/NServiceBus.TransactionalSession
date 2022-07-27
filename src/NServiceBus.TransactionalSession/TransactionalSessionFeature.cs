namespace NServiceBus.TransactionalSession
{
    using System.Threading.Tasks;
    using Features;
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
            var physicalLocalQueueAddress = context.Settings.LocalAddress();

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Container.ConfigureComponent<ITransactionalSession>(builder =>
            {
                if (isOutboxEnabled)
                {
                    return new OutboxTransactionalSession(
                        builder.Build<IOutboxStorage>(),
                        builder.Build<CompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        builder.Build<IDispatchMessages>(),
                        physicalLocalQueueAddress
                        );
                }

                return new TransactionalSession(
                    builder.Build<CompletableSynchronizedStorageSession>(),
                    sessionCaptureTask.CapturedSession,
                    builder.Build<IDispatchMessages>());
            }, DependencyLifecycle.InstancePerUnitOfWork);

            if (isOutboxEnabled)
            {
                context.Pipeline.Register(builder => new TransactionalSessionDelayControlMessageBehavior(builder.Build<IDispatchMessages>(),
                    physicalLocalQueueAddress
                ), "Transaction commit control message delay behavior");

                context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
                    "Transaction commit control message delay acknowledgement behavior");
            }
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public IMessageSession CapturedSession { get; set; }

            protected override Task OnStart(IMessageSession session)
            {
                CapturedSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session) => Task.CompletedTask;
        }
    }
}