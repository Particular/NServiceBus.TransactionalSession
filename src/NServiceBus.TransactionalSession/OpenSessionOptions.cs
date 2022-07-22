namespace NServiceBus.TransactionalSession
{
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
    }
}