namespace NServiceBus.TransactionalSession;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for NHibernate.
/// </summary>
public static class NHibernateSupport
{
    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a NHibernate session.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns></returns>
    public static Task OpenNHibernateSession(this ITransactionalSession session, OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var endpointQualifiedMessageIdKeyName = "NServiceBus.Persistence.NHibernate.EndpointQualifiedMessageId";

        if (session.PersisterSpecificOptions.TryGet(out string endpointName))
        {
            options ??= new OpenSessionOptions();

            var endpointQualifiedMessageId = $"{endpointName}/{options.SessionId}";

            options.Extensions.Set(endpointQualifiedMessageIdKeyName, endpointQualifiedMessageId);
            options.Metadata.Add(endpointQualifiedMessageIdKeyName, endpointQualifiedMessageId);
        }

        return session.Open(options, cancellationToken);
    }
}