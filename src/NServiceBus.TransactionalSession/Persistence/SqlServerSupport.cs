// namespace NServiceBus.TransactionalSession;
//
// using System;
// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using Transport;
//
// /// <summary>
// /// Provides <see cref="ITransactionalSession"/> support for SQL Server.
// /// </summary>
// public static class SqlServerSupport
// {
//     /// <summary>
//     /// Opens a <see cref="ITransactionalSession"/> connected to a SQL database.
//     /// </summary>
//     /// <param name="session">The session to open</param>
//     /// <param name="tenantIdHeaderName">The header key name used to determine the tenant id.</param>
//     /// <param name="tenantId">The tenant id used for this session.</param>
//     /// <param name="options">The specific options used to open this session.</param>
//     /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
//     public static Task OpenSqlSession(this ITransactionalSession session, string tenantIdHeaderName = null, string tenantId = null, OpenSessionOptions options = null,
//         CancellationToken cancellationToken = default)
//     {
//         Guard.AgainstNull(nameof(session), session);
//
//         options ??= new OpenSessionOptions();
//
//         var headers = new Dictionary<string, string>();
//
//         if (tenantIdHeaderName != null && tenantId != null)
//         {
//             headers.Add(tenantIdHeaderName, tenantId);
//             options.Metadata.Add(tenantIdHeaderName, tenantId);
//         }
//
//         options.Extensions.Set(new IncomingMessage(options.SessionId, headers, Array.Empty<byte>()));
//
//         return session.Open(options, cancellationToken);
//     }
// }