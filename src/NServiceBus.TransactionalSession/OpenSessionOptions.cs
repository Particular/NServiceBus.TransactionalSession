namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using Extensibility;

    /// <summary>
    /// Allows the users to control how the transaction session behaves.
    /// </summary>
    /// <remarks>
    /// The behavior of this class is exposed via extension methods.
    /// </remarks>
    public class OpenSessionOptions : IExtendable
    {
        /// <summary>
        /// Options extensions.
        /// </summary>
        public ContextBag Extensions => extensions ??= new ContextBag();

        internal bool HasExtensions => extensions != null;

        /// <summary>
        /// Session metadata that gets added during the session commit operation.
        /// </summary>
        public IDictionary<string, string> Metadata => metadata ??= new Dictionary<string, string>();

        internal bool HasMetadata => metadata != null;

        /// <summary>
        /// The maximum duration the transaction is allowed to attempt to atomically commit.
        /// </summary>
        /// <remarks>The actual total transaction time observed might be longer, taking into account delays in the transport due to latency, delayed delivery and more.</remarks>
        public TimeSpan MaximumCommitDuration { get; set; } = TimeSpan.FromSeconds(15);

        internal TimeSpan CommitDelayIncrement { get; set; } = TimeSpan.FromSeconds(2);

        Dictionary<string, string> metadata;
        ContextBag extensions;

    }
}