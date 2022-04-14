namespace NServiceBus.TransactionalSession
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using System.Threading;
    using System.Threading.Tasks;
    using Transport;

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var localQueueAddress = context.LocalQueueAddress();

            context.Services.AddSingleton<DumpingGround>();
            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            context.RegisterStartupTask(provider => new TransactionalSessionStartupTask(
                localQueueAddress,
                isOutboxEnabled,
                provider.GetRequiredService<ITransportAddressResolver>(),
                provider.GetRequiredService<DumpingGround>()));
            context.Services.AddScoped<ITransactionalSession, TransactionalSession>();

        }

        public class TransactionalSessionStartupTask : FeatureStartupTask
        {
            private readonly QueueAddress localQueueAddress;
            private readonly bool isOutboxEnabled;
            private readonly ITransportAddressResolver transportAddressResolver;
            readonly DumpingGround sessionHolder;

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

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }
        }
    }
}