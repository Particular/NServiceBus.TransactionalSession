namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDelayedDelivery { get; } = true;
        public bool SupportsDtc { get; } = false;
        public bool SupportsCrossQueueTransactions { get; } = true;
        public bool SupportsPurgeOnStartup { get; } = true;
        public bool SupportsNativePubSub { get; } = true;
        public bool SupportsOutbox { get; } = false;

        public IConfigureEndpointTestExecution CreateTransportConfiguration() =>
            new ConfigureEndpointAcceptanceTestingTransport(SupportsNativePubSub, SupportsDelayedDelivery,
                TransportTransactionMode.ReceiveOnly);

        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() =>
            new ConfigureEndpointAcceptanceTestingPersistence();
    }
}