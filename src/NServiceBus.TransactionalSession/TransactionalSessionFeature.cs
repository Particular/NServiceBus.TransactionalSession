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
    /// Provides support for  <see cref="IBatchedMessageSession"/> and <see cref="ITransactionalSession" />.
    /// </summary>
    public class TransactionalSessionFeature : Feature
    {
        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);

            if (isOutboxEnabled)
            {
                if (context.Settings.TryGet("NServiceBus.Persistence.CosmosDB.OutboxStorage", out FeatureState cosmosSate) && cosmosSate == FeatureState.Active)
                {
                    context.Pipeline.Register(new CosmosDBSupport.CosmosControlMessageBehavior(), "Configures the partition key and container for the ITransactionalSession dispatch phase");
                }

                context.Services.AddScoped(sp =>
                {
                    var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                        .ToTransportAddress(localQueueAddress);

                    ITransactionalSession transactionalSession = new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        physicalLocalQueueAddress
                    );

                    if (context.Settings.TryGet("NServiceBus.Features.NHibernateOutbox", out FeatureState nhState) &&
                        nhState == FeatureState.Active)
                    {
                        transactionalSession.PersisterSpecificOptions.Set(context.Settings.EndpointName());
                    }

                    if (context.Settings.TryGet("NServiceBus.Persistence.AzureTable.OutboxStorage",
                            out FeatureState atState) && atState == FeatureState.Active)
                    {
                        transactionalSession.PersisterSpecificOptions.Set(
                            sp.GetRequiredService(Type.GetType(AzureTableSupport.TableHolderResolverAssemblyQualifiedTypeName)));
                    }

                    return transactionalSession;
                });

                context.Pipeline.Register(sp => new TransactionalSessionDelayControlMessageBehavior(sp.GetRequiredService<IMessageDispatcher>(),
                    sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress)
                ), "Transaction commit control message delay behavior");

                context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
                    "Transaction commit control message delay acknowledgement behavior");
            }
            else
            {
                context.Services.AddScoped<IBatchedMessageSession>(sp => new BatchedMessageSession(
                    sessionCaptureTask.CapturedSession,
                    sp.GetRequiredService<IMessageDispatcher>()));
            }
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public IMessageSession CapturedSession { get; set; }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new())
            {
                CapturedSession = session;
                return Task.CompletedTask;
            }
            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new()) => Task.CompletedTask;
        }
    }
}