namespace NServiceBus.TransactionalSession;

using System;
using System.Threading.Tasks;
using Pipeline;

static class CosmosDBSupport
{
    public const string PartitionKeyTypeFullName = "Microsoft.Azure.Cosmos.PartitionKey";
    public const string ContainerInformationTypeFullName = "NServiceBus.ContainerInformation";

    //TODO support other partitionkey values
    public static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        //TODO make lazy for caching
        var partitionKeyType = Type.GetType("Microsoft.Azure.Cosmos.PartitionKey, Microsoft.Azure.Cosmos.Client, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKeyString);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return partitionKeyInstance;
    }

    public static object CreateContainerInformationInstance(string containerName, string partitionKeyPath)
    {
        var partitionKeyPathType = Type.GetType("NServiceBus.PartitionKeyPath, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var partitionKeyPathInstance = Activator.CreateInstance(partitionKeyPathType, partitionKeyPath);
        var containerInformationType = Type.GetType("NServiceBus.ContainerInformation, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var containerInformationInstance = Activator.CreateInstance(containerInformationType, containerName, partitionKeyPathInstance);
        return containerInformationInstance;
    }

    public class CosmosControlMessageBehavior : Behavior<ITransportReceiveContext>
    {
        public const string PartitionKeyHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKey";
        public const string ContainerNameHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerName";
        public const string ContainerPartitionKeyPathHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerPartitionKeyPath";

        public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            if (context.Message.Headers.TryGetValue(PartitionKeyHeaderKey, out var partitionKeyString))
            {
                var partitionKeyInstance = CreatePartitionKeyInstance(partitionKeyString);
                context.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
            }

            if (context.Message.Headers.TryGetValue(ContainerNameHeaderKey, out string containerName)
                && context.Message.Headers.TryGetValue(ContainerPartitionKeyPathHeaderKey, out string partitionKeyPath))
            {
                var containerInformationInstance = CreateContainerInformationInstance(containerName, partitionKeyPath);
                context.Extensions.Set(ContainerInformationTypeFullName, containerInformationInstance);
            }

            return next();
        }
    }
}