namespace NServiceBus.TransactionalSession
{
    using Features;
    using SynchronizedStorage = AcceptanceTesting.SynchronizedStorage;

    sealed class CustomTestingPersistenceTransactionalSession : Feature
    {
        public CustomTestingPersistenceTransactionalSession()
        {
            Defaults(s =>
            {
                s.EnableFeatureByDefault<TransactionalSession>();
            });

            DependsOn<SynchronizedStorage>();
            DependsOn<TransactionalSession>();
        }
        protected override void Setup(FeatureConfigurationContext context)
        {
        }
    }
}