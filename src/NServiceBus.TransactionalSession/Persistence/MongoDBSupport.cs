namespace NServiceBus.TransactionalSession;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for MongoDB.
/// </summary>
public static class MongoDBSupport
{
    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a MongoDB database.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    public static Task OpenMongoDBSession(this ITransactionalSession session, OpenSessionOptions options = null, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(nameof(session), session);

        return session.Open(options, cancellationToken);
    }
}