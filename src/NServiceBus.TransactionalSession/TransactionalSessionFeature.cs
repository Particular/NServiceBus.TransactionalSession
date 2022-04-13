namespace NServiceBus.TransactionalSession
{
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using System.Threading;
    using System.Threading.Tasks;

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<MessageSessionHolder>();
            context.RegisterStartupTask(provider => new TransactionalSessionStartupTask(provider.GetRequiredService<MessageSessionHolder>()));
            context.Services.AddScoped<ITransactionalSession, TransactionalSession>();
        }

        public class TransactionalSessionStartupTask : FeatureStartupTask
        {
            readonly MessageSessionHolder sessionHolder;

            public TransactionalSessionStartupTask(MessageSessionHolder sessionHolder)
            {
                this.sessionHolder = sessionHolder;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                sessionHolder.Instance = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }
        }
    }
}