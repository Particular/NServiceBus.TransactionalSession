namespace NServiceBus.TransactionalSession;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TODO
/// </summary>
public static class OpenSessionExtensions
{
    //TODO also do for the double key overload?
    //TODO also support custom container configuration
    /// <summary>
    /// TODO
    /// </summary>
    /// <returns></returns>
    public static Task OpenCosmosDBSession(this ITransactionalSession session, string partitionKey, OpenSessionOptions options = null, CancellationToken cancellationToken = default)
    {
        options ??= new OpenSessionOptions();
        var partitionKeyType = Type.GetType("Microsoft.Azure.Cosmos.PartitionKey, Microsoft.Azure.Cosmos.Client, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKey);
        options.Extensions.Set("Microsoft.Azure.Cosmos.PartitionKey", partitionKeyInstance);
        options.Metadata.Add("NServiceBus.TxSession.CosmosDB.PartitionKey", partitionKey);

        return session.Open(options, cancellationToken);
    }
}