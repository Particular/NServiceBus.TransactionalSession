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
    /// <summary>
    /// TODO
    /// </summary>
    /// <returns></returns>
    public static Task OpenCosmosDBSession(this ITransactionalSession session, string partitionKey, (string containerName, string partitionKeyPath) container = default, OpenSessionOptions options = null, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullAndEmpty(nameof(partitionKey), partitionKey);

        options ??= new OpenSessionOptions();
        var partitionKeyInstance = CosmosDBSupport.CreatePartitionKeyInstance(partitionKey);
        options.Extensions.Set(CosmosDBSupport.PartitionKeyTypeFullName, partitionKeyInstance);
        options.Metadata.Add(CosmosDBSupport.CosmosControlMessageBehavior.PartitionKeyHeaderKey, partitionKey);

        if (container != default)
        {
            if (string.IsNullOrWhiteSpace(container.containerName) && string.IsNullOrWhiteSpace(container.partitionKeyPath))
            {
                throw new ArgumentException("Invalid CosmosDB container definition", nameof(container));
            }

            var containerInformation = CosmosDBSupport.CreateContainerInformationInstance(container.containerName, container.partitionKeyPath);
            options.Extensions.Set(CosmosDBSupport.ContainerInformationTypeFullName, containerInformation);
            options.Metadata.Add(CosmosDBSupport.CosmosControlMessageBehavior.ContainerNameHeaderKey, container.containerName);
            options.Metadata.Add(CosmosDBSupport.CosmosControlMessageBehavior.ContainerPartitionKeyPathHeaderKey, container.partitionKeyPath);
        }

        return session.Open(options, cancellationToken);
    }
}