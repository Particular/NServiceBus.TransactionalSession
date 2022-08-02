namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Transport;

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
    public static Task OpenCosmosDBSession(this ITransactionalSession session,
        string partitionKey,
        (string containerName, string partitionKeyPath) container = default,
        OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="session"></param>
    /// <param name="multiTenantConnectionContext"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task OpenRavenDBSession(this ITransactionalSession session,
        IDictionary<string, string> multiTenantConnectionContext = null,
        OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OpenSessionOptions();
        var headers = multiTenantConnectionContext != null ? new Dictionary<string, string>(multiTenantConnectionContext) : new Dictionary<string, string>(0);
        // order matters because IncomingMessage is modifying the headers
        foreach (var header in headers)
        {
            options.Metadata.Add(header.Key, header.Value);
        }

        options.Extensions.Set(new IncomingMessage("do not use", headers, ReadOnlyMemory<byte>.Empty));
        return session.Open(options, cancellationToken);
    }

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="session"></param>
    /// <param name="tenantIdHeaderName"></param>
    /// <param name="tenantId"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task OpenSqlSession(this ITransactionalSession session, string tenantIdHeaderName = null, string tenantId = null, OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OpenSessionOptions();
        options.CustomSessionId = Guid.NewGuid().ToString();

        var headers = new Dictionary<string, string>();

        if (tenantIdHeaderName != null)
        {
            headers.Add(tenantIdHeaderName, tenantId);
            options.Metadata.Add(tenantIdHeaderName, tenantId);
        }

        options.Extensions.Set(new IncomingMessage(options.CustomSessionId, headers, Array.Empty<byte>()));

        return session.Open(options, cancellationToken);
    }
}