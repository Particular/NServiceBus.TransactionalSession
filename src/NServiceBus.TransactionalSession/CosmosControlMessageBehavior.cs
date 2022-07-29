namespace NServiceBus.TransactionalSession;

using System;
using System.Threading.Tasks;
using Pipeline;

class CosmosControlMessageBehavior : Behavior<ITransportReceiveContext>
{
    const string CosmosPartitionKeyHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKey";

    public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
    {
        if (context.Message.Headers.TryGetValue(CosmosPartitionKeyHeaderKey, out var partitionKeyString))
        {
            var partitionKeyInstance = CreatePartitionKeyInstance(partitionKeyString);
            context.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
        }

        return next();
    }

    const string PartitionKeyTypeFullName = "Microsoft.Azure.Cosmos.PartitionKey";

    //TODO support other partitionkey values
    static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        //TODO make lazy for caching
        var partitionKeyType = Type.GetType("Microsoft.Azure.Cosmos.PartitionKey, Microsoft.Azure.Cosmos.Client, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKeyString);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return partitionKeyInstance;
    }
}