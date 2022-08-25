namespace NServiceBus.TransactionalSession;

using System;
using System.Globalization;
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

    static readonly Lazy<Type> PartitionKeyType = new(() =>
        Type.GetType("Microsoft.Azure.Cosmos.PartitionKey, Microsoft.Azure.Cosmos.Client, Culture=neutral, PublicKeyToken=31bf3856ad364e35"), LazyThreadSafetyMode.PublicationOnly);

    static readonly Lazy<Type> PartitionKeyPathType = new(() =>
        Type.GetType("NServiceBus.PartitionKeyPath, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c"), LazyThreadSafetyMode.PublicationOnly);

    static readonly Lazy<Type> ContainerInformationType = new(() =>
        Type.GetType("NServiceBus.ContainerInformation, NServiceBus.Persistence.CosmosDB, Culture=neutral, PublicKeyToken=9fc386479f8a226c"), LazyThreadSafetyMode.PublicationOnly);

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
        options.Metadata.Add(CosmosControlMessageBehavior.PartitionKeyStringHeaderKey, partitionKey);

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

    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a CosmosDB database.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="partitionKey">The partition key value for the transactional batch</param>
    /// <param name="container">Optional container information (container name and container partition key path) to use. Uses the configured default container otherwise.</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    public static Task OpenCosmosDBSession(this ITransactionalSession session,
        double partitionKey,
        (string containerName, string partitionKeyPath) container = default,
        OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNull(nameof(session), session);

        options ??= new OpenSessionOptions();
        var partitionKeyInstance = CreatePartitionKeyInstance(partitionKey);
        options.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
        // roundtrip values for doubles should use "G17" instead of "R"
        options.Metadata.Add(CosmosControlMessageBehavior.PartitionKeyDoubleHeaderKey, partitionKey.ToString("G17", CultureInfo.InvariantCulture));

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

    static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        try
        {
            var partitionKeyInstance = Activator.CreateInstance(PartitionKeyType.Value, partitionKeyString);

            return partitionKeyInstance;
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to create a `Microsoft.Azure.Cosmos.PartitionKey` instance for value '{partitionKeyString}'", e);
        }
    }

    static object CreatePartitionKeyInstance(double partitionKeyDouble)
    {
        try
        {
            var partitionKeyInstance = Activator.CreateInstance(PartitionKeyType.Value, partitionKeyDouble);

            return partitionKeyInstance;
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to create a `Microsoft.Azure.Cosmos.PartitionKey` instance for value '{partitionKeyDouble}'", e);
        }
    }

    static object CreateContainerInformationInstance(string containerName, string partitionKeyPath)
    {
        try
        {
            var partitionKeyPathInstance = Activator.CreateInstance(PartitionKeyPathType.Value, partitionKeyPath);
            var containerInformationInstance = Activator.CreateInstance(ContainerInformationType.Value, containerName, partitionKeyPathInstance);
            return containerInformationInstance;
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to create a `NServiceBus.ContainerInformation` instance for value '{partitionKeyPath}'", e);

        }
    }

    internal class CosmosControlMessageBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        public const string PartitionKeyStringHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKeyString";
        public const string PartitionKeyDoubleHeaderKey = "NServiceBus.TxSession.CosmosDB.PartitionKeyDouble";
        public const string ContainerNameHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerName";
        public const string ContainerPartitionKeyPathHeaderKey = "NServiceBus.TxSession.CosmosDB.ContainerPartitionKeyPath";

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (context.Message.Headers.TryGetValue(PartitionKeyStringHeaderKey, out var partitionKeyString))
            {
                var partitionKeyInstance = CreatePartitionKeyInstance(partitionKeyString);
                context.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
            }
            else if (context.Message.Headers.TryGetValue(PartitionKeyDoubleHeaderKey, out var partitionKeyDoubleString))
            {
                var partitionKeyInstance = CreatePartitionKeyInstance(double.Parse(partitionKeyDoubleString, CultureInfo.InvariantCulture));
                context.Extensions.Set(PartitionKeyTypeFullName, partitionKeyInstance);
            }

            if (context.Message.Headers.TryGetValue(ContainerNameHeaderKey, out string containerName)
                && context.Message.Headers.TryGetValue(ContainerPartitionKeyPathHeaderKey, out string partitionKeyPath))
            {
                var containerInformationInstance = CreateContainerInformationInstance(containerName, partitionKeyPath);
                context.Extensions.Set(ContainerInformationTypeFullName, containerInformationInstance);
            }

            return next(context);
        }
    }
}