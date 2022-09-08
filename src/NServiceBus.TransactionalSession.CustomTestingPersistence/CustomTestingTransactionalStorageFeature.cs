namespace NServiceBus.AcceptanceTesting
{
    using Features;

    class CustomTestingTransactionalStorageFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<CustomTestingSynchronizedStorage>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<CustomTestingSynchronizedStorageAdapter>(DependencyLifecycle.SingleInstance);
        }
    }
}