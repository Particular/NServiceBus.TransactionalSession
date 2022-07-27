namespace NServiceBus.TransactionalSession
{
    /// <summary>
    /// Enables the transactional session feature.
    /// </summary>
    public static class TransactionalSessionConfigurationExtensions
    {
        /// <summary>
        /// Enables the transactional session for this endpoint.
        /// </summary>
        /// <param name="endpointConfiguration">Endpoint configuration.</param>
        public static void EnableTransactionalSession(this EndpointConfiguration endpointConfiguration) =>
            endpointConfiguration.EnableFeature<TransactionalSessionFeature>();
    }
}