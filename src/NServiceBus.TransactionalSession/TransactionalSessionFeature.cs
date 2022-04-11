namespace NServiceBus.TransactionalSession
{
    using Features;
    using System.Threading;
    using System.Threading.Tasks;

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            throw new System.NotImplementedException();
        }

        class RegisterSessionStartupTask : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                throw new System.NotImplementedException();
            }
        }
    }
}