namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using Extensibility;

    /// <summary>
    /// Allows the users to control how the transaction session behaves.
    /// </summary>
    public abstract class OpenSessionOptions
    {
        /// <summary>
        /// Options extensions.
        /// </summary>
        protected internal ContextBag Extensions { get; } = new();

        /// <summary>
        /// The session id
        /// </summary>
        /// <remarks>By default it uses a new Guid</remarks>
        protected internal string SessionId { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Session metadata that gets added during the session commit operation.
        /// </summary>
        public IDictionary<string, string> Metadata => metadata ??= [];

        internal bool HasMetadata => metadata != null;

        /// <summary>
        /// The maximum duration the transaction is allowed to attempt to atomically commit.
        /// </summary>
        /// <remarks>The actual total transaction time observed might be longer, taking into account delays in the transport due to latency, delayed delivery and more.</remarks>
        /// <remarks>Defaults to <code>TimeSpan.FromSeconds(15)</code></remarks>
        public TimeSpan MaximumCommitDuration { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The time increment used to delay the commit of the transactional session when the outbox record is not yet in the storage.
        /// </summary>
        /// <remarks>Defaults to <code>TimeSpan.FromSeconds(2)</code></remarks>
        public TimeSpan CommitDelayIncrement { get; set; } = TimeSpan.FromSeconds(2);

        Dictionary<string, string> metadata;
    }
}