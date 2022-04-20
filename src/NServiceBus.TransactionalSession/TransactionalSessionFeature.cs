namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Transport;

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            context.Services.AddSingleton<DumpingGround>();
            bool isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            context.RegisterStartupTask(provider => new TransactionalSessionStartupTask(
                localQueueAddress,
                isOutboxEnabled,
                provider.GetRequiredService<ITransportAddressResolver>(),
                provider.GetRequiredService<DumpingGround>()));
            context.Services.AddScoped<ITransactionalSession, TransactionalSession>();
        }

        public class TransactionalSessionStartupTask : FeatureStartupTask
        {
            public TransactionalSessionStartupTask(QueueAddress localQueueAddress, bool isOutboxEnabled,
                ITransportAddressResolver transportAddressResolver, DumpingGround sessionHolder)
            {
                this.localQueueAddress = localQueueAddress;
                this.isOutboxEnabled = isOutboxEnabled;
                this.transportAddressResolver = transportAddressResolver;
                this.sessionHolder = sessionHolder;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                sessionHolder.Instance = session;
                sessionHolder.PhysicalQueueAddress = transportAddressResolver.ToTransportAddress(localQueueAddress);
                sessionHolder.IsOutboxEnabled = isOutboxEnabled;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

            readonly QueueAddress localQueueAddress;
            readonly bool isOutboxEnabled;
            readonly ITransportAddressResolver transportAddressResolver;
            readonly DumpingGround sessionHolder;
        }
    }
}