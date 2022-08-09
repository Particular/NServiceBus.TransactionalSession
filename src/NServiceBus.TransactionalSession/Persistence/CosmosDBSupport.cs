namespace NServiceBus.TransactionalSession;

using System;
using System.Threading;
using System.Threading.Tasks;
using Pipeline;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for CosmosDB.
/// </summary>
public static class CosmosDBSupport
{
    const string PartitionKeyTypeFullName = "Microsoft.Azure.Cosmos.PartitionKey";
    const string ContainerInformationTypeFullName = "NServiceBus.ContainerInformation";

    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a CosmosDB database.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="partitionKey">The partition key value for the transactional batch</param>
    /// <param name="container">Optional container information (container name and container partition key path) to use. Uses the configured default container otherwise.</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    public static Task OpenCosmosDBSession(this ITransactionalSession session,
        string partitionKey,
        (string containerName, string partitionKeyPath) container = default,
        OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(nameof(session), session);
        Guard.AgainstNullAndEmpty(nameof(partitionKey), partitionKey);

        options ??= new OpenSessionOptions();
        var partitionKeyInstance = CreatePartitionKeyInstance(partitionKey);
        options.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
        options.Metadata.Add(CosmosControlMessageBehavior.PartitionKeyHeaderKey, partitionKey);

        if (container != default)
        {
            if (string.IsNullOrWhiteSpace(container.containerName) && string.IsNullOrWhiteSpace(container.partitionKeyPath))
            {
                throw new ArgumentException("Invalid CosmosDB container definition", nameof(container));
            }

            var containerInformation = CreateContainerInformationInstance(container.containerName, container.partitionKeyPath);
            options.Extensions.Set(ContainerInformationTypeFullName, containerInformation);
            options.Metadata.Add(CosmosControlMessageBehavior.ContainerNameHeaderKey, container.containerName);
            options.Metadata.Add(CosmosControlMessageBehavior.ContainerPartitionKeyPathHeaderKey, container.partitionKeyPath);
        }

        return session.Open(options, cancellationToken);
    }

    //TODO support other partitionkey values
    static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        //TODO make lazy for caching
        var partitionKeyType = Type.GetType("Microsoft.Azure.Cosmos.PartitionKey, Microsoft.Azure.Cosmos.Client, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKeyString);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return partitionKeyInstance;
    }

    static object CreateContainerInformationInstance(string containerName, string partitionKeyPath)
    {
        var partitionKeyPathType = Type.GetType("NServiceBus.PartitionKeyPath, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var partitionKeyPathInstance = Activator.CreateInstance(partitionKeyPathType, partitionKeyPath);
        var containerInformationType = Type.GetType("NServiceBus.ContainerInformation, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var containerInformationInstance = Activator.CreateInstance(containerInformationType, containerName, partitionKeyPathInstance);
        return containerInformationInstance;
    }

    internal class CosmosControlMessageBehavior : Behavior<ITransportReceiveContext>
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