namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDelayedDelivery => true;
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => true;
        public bool SupportsNativeDeferral => true;
        public bool SupportsOutbox => true;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() =>
            new ConfigureEndpointAcceptanceTestingTransport(SupportsNativePubSub, SupportsDelayedDelivery);

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() =>
            new ConfigureEndpointCustomTestingPersistence();
    }
}