namespace NServiceBus.TransactionalSession
{
    using System;
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

            // Due to ordering issues it might not always be possible to check whether the feature is active. Checking for enabled and active is the safest bet since the behaviors use defensive techniques to get the values
            if (context.Settings.TryGet("NServiceBus.Persistence.CosmosDB.SynchronizedStorage", out FeatureState cosmosSate) && cosmosSate is FeatureState.Enabled or FeatureState.Active)
            {
                context.Pipeline.Register(new CosmosDBSupport.CosmosControlMessageBehavior(), "Propagates control message header values to PartitionKeys and ContainerInformation when necessary.");
            }

            // Due to ordering issues it might not always be possible to check whether the feature is active. Checking for enabled and active is the safest bet since the behaviors use defensive techniques to get the values
            if (context.Settings.TryGet("NServiceBus.Persistence.AzureTable.SynchronizedStorage", out FeatureState azureTableState) && azureTableState is FeatureState.Enabled or FeatureState.Active)
            {
                context.Pipeline.Register(new AzureTableSupport.AzureTableControlMessageBehavior(), "Propagates control message header values to TableEntityPartitionKeys and TableInformation when necessary.");
            }

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped(sp =>
            {
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(localQueueAddress);

                ITransactionalSession transactionalSession;

                if (isOutboxEnabled)
                {
                    transactionalSession = new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        physicalLocalQueueAddress
                        );
                }
                else
                {
                    transactionalSession = new TransactionalSession(
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>());
                }

                if (context.Settings.TryGet("NServiceBus.Features.NHibernateOutbox", out FeatureState nhState) && nhState == FeatureState.Active)
                {
                    transactionalSession.PersisterSpecificOptions.Set(context.Settings.EndpointName());
                }

                if (context.Settings.TryGet("NServiceBus.Persistence.AzureTable.SynchronizedStorage", out FeatureState atState) && atState == FeatureState.Active)
                {
                    transactionalSession.PersisterSpecificOptions.Set(sp.GetRequiredService(Type.GetType(AzureTableSupport.TableHolderResolverAssemblyQualifiedTypeName)));
                }

                return transactionalSession;
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