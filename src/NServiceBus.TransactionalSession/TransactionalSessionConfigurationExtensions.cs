namespace NServiceBus
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Features;

    /// <summary>
    /// Enables the transactional session feature.
    /// </summary>
    public static class TransactionalSessionConfigurationExtensions
    {
        /// <summary>
        /// Enables the transactional session for this endpoint.
        /// </summary>
        /// <param name="endpointConfiguration">Endpoint configuration.</param>
        /// <param name="maxCommitDelay">Maximum amount of time to wait before aborting the transactional session.</param>
        /// <param name="commitDelayIncrement">Delay between the attempts to push outgoing messages from the transactional session.</param>
        public static void EnableTransactionalSession(this EndpointConfiguration endpointConfiguration, TimeSpan? maxCommitDelay = null, TimeSpan? commitDelayIncrement = null)
        {
            endpointConfiguration.EnableFeature<TransactionalSessionFeature>();
            if (maxCommitDelay.HasValue)
            {
                endpointConfiguration.GetSettings().Set(TransactionalSessionFeature.MaxCommitDelaySettingsKey, maxCommitDelay);
            }
            if (commitDelayIncrement.HasValue)
            {
                endpointConfiguration.GetSettings().Set(TransactionalSessionFeature.CommitDelayIncrementSettingsKey, commitDelayIncrement);
            }
        }
    }
}