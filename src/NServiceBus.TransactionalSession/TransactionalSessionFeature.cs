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

            // // Due to ordering issues it might not always be possible to check whether the feature is active. Checking for enabled and active is the safest bet since the behaviors use defensive techniques to get the values
            // if (context.Settings.TryGet("NServiceBus.Persistence.CosmosDB.SynchronizedStorage", out FeatureState cosmosSate) && cosmosSate is FeatureState.Enabled or FeatureState.Active)
            // {
            //     context.Pipeline.Register(new CosmosDBSupport.CosmosControlMessageBehavior(), "Propagates control message header values to PartitionKeys and ContainerInformation when necessary.");
            // }
            //
            // // Due to ordering issues it might not always be possible to check whether the feature is active. Checking for enabled and active is the safest bet since the behaviors use defensive techniques to get the values
            // if (context.Settings.TryGet("NServiceBus.Persistence.AzureTable.SynchronizedStorage", out FeatureState azureTableState) && azureTableState is FeatureState.Enabled or FeatureState.Active)
            // {
            //     context.Pipeline.Register(new AzureTableSupport.AzureTableControlMessageBehavior(), "Propagates control message header values to TableEntityPartitionKeys and TableInformation when necessary.");
            // }

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Container.ConfigureComponent(builder =>
            {
                ITransactionalSession transactionalSession;

                if (isOutboxEnabled)
                {
                    transactionalSession = new OutboxTransactionalSession(
                        builder.Build<IOutboxStorage>(),
                        builder.Build<CompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        builder.Build<IDispatchMessages>(),
                        physicalLocalQueueAddress
                        );
                }
                else
                {
                    transactionalSession = new TransactionalSession(
                        builder.Build<CompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        builder.Build<IDispatchMessages>());
                }

                // if (context.Settings.TryGet("NServiceBus.Features.NHibernateOutbox", out FeatureState nhState) && nhState == FeatureState.Active)
                // {
                //     transactionalSession.PersisterSpecificOptions.Set(context.Settings.EndpointName());
                // }
                //
                // if (context.Settings.TryGet("NServiceBus.Persistence.AzureTable.SynchronizedStorage", out FeatureState atState) && atState == FeatureState.Active)
                // {
                //     transactionalSession.PersisterSpecificOptions.Set(builder.Build(Type.GetType(AzureTableSupport.TableHolderResolverAssemblyQualifiedTypeName)));
                // }

                return transactionalSession;
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