namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Transport;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for RavenDB.
/// </summary>
public static class RavenDBSupport
{
    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a RavenDB database.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="multiTenantConnectionContext">An optional dictionary that will be passed to the multi-tenant supporting `SetMessageToDatabaseMappingConvention` callback to determine the tenant information.</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    public static Task OpenRavenDBSession(this ITransactionalSession session,
        IDictionary<string, string> multiTenantConnectionContext = null,
        OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(nameof(session), session);

        options ??= new OpenSessionOptions();
        var headers = multiTenantConnectionContext != null ? new Dictionary<string, string>(multiTenantConnectionContext) : new Dictionary<string, string>(0);

        // order matters because instantiating IncomingMessage is modifying the headers
        foreach (var header in headers)
        {
            options.Metadata.Add(header.Key, header.Value);
        }

        options.Extensions.Set(new IncomingMessage(options.SessionId, headers, Array.Empty<byte>()));
        return session.Open(options, cancellationToken);
    }

}