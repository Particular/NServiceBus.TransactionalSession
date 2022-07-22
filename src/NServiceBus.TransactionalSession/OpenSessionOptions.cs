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
        Dictionary<string, string> metadata;
        ContextBag extensions;

        /// <summary>
        /// Options extensions.
        /// </summary>
        public ContextBag Extensions => extensions ??= new ContextBag();

        internal bool HasExtensions => extensions != null;

        /// <summary>
        /// Session metadata.
        /// </summary>
        public IDictionary<string, string> Metadata => metadata ??= new Dictionary<string, string>();

        internal bool HasMetadata => metadata != null;

        /// <summary>
        /// TBD
        /// </summary>
        public TimeSpan MaximumCommitDuration { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        ///
        /// </summary>
        internal TimeSpan CommitDelayIncrement { get; set; } = TimeSpan.FromSeconds(2);
    }
}