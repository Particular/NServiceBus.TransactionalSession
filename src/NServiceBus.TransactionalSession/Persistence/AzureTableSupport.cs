namespace NServiceBus.TransactionalSession;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Pipeline;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for Azure Table Storage.
/// </summary>
public static class AzureTableSupport
{
    internal static string TableHolderResolverAssemblyQualifiedTypeName = "NServiceBus.Persistence.AzureTable.TableHolderResolver, NServiceBus.Persistence.AzureTable";

    const string TableInformationTypeName = "NServiceBus.TableInformation";
    const string TableEntityPartitionKeyTypeName = "NServiceBus.TableEntityPartitionKey";
    const string SetAsDispatchedHolderTypeName = "NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder";

    static readonly Lazy<Type> TableInformationType =
        new(() => Type.GetType("NServiceBus.TableInformation, NServiceBus.Persistence.AzureTable, Culture = neutral, PublicKeyToken = 9fc386479f8a226c"), LazyThreadSafetyMode.PublicationOnly);

    static readonly Lazy<Type> TableEntityPartitionKeyType =
        new(() => Type.GetType("NServiceBus.TableEntityPartitionKey, NServiceBus.Persistence.AzureTable, Culture=neutral, PublicKeyToken=9fc386479f8a226c"), LazyThreadSafetyMode.PublicationOnly);

    static readonly Lazy<Type> SetAsDispatchedHolderType =
        new(() => Type.GetType("NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder, NServiceBus.Persistence.AzureTable, Culture=neutral, PublicKeyToken=9fc386479f8a226c"), LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a AzureTable storage.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="partitionKey">The specific partition used for this session.</param>
    /// <param name="tableName">The optional table name to be used to be used.</param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns></returns>
    public static Task OpenAzureTableSession(this ITransactionalSession session, string partitionKey, string tableName = null, OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullAndEmpty(nameof(partitionKey), value: "Partition key value cannot be null.");

        options ??= new OpenSessionOptions();

        options.Metadata.Add(AzureTableControlMessageBehavior.PartitionKeyStringHeaderKey, partitionKey);

        var partitionKeyInstance = CreatePartitionKeyInstance(partitionKey);
        options.Extensions.Set(TableEntityPartitionKeyTypeName, partitionKeyInstance);

        if (tableName != null)
        {
            options.Extensions.Set(TableInformationTypeName, CreateTableInformationInstance(tableName));
            options.Metadata.Add(AzureTableControlMessageBehavior.TableInformationHeaderKey, tableName);
        }

        var tableHolderResolver = session.PersisterSpecificOptions.Get<object>();
        options.Extensions.Set(SetAsDispatchedHolderTypeName, CreateSetAsDispatchedInstance(tableHolderResolver, partitionKeyInstance, options.Extensions));

        return session.Open(options, cancellationToken);
    }

    static object CreateTableInformationInstance(string tableName)
    {
        try
        {
            var tableInformationInstance = Activator.CreateInstance(TableInformationType.Value, tableName);

            return tableInformationInstance;
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to create a `NServiceBus.TableInformation` instance for value '{tableName}'", e);
        }
    }

    static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        try
        {
            var partitionKeyInstance = Activator.CreateInstance(TableEntityPartitionKeyType.Value, partitionKeyString);

            return partitionKeyInstance;
        }
        catch (Exception e)
        {
            throw new Exception($"Unable to create a `NServiceBus.TableEntityPartitionKey` instance for value '{partitionKeyString}'", e);
        }
    }

    static object CreateSetAsDispatchedInstance(object tableHolderResolver, object partitionKey, ContextBag context)
    {
        try
        {
            Type setAsDispatchedHolderType = SetAsDispatchedHolderType.Value;
            var setAsDispatchedInstance = Activator.CreateInstance(setAsDispatchedHolderType);

            var resolveAndSetIfAvailableMethod = tableHolderResolver.GetType().GetMethod("ResolveAndSetIfAvailable");
            var tableHolderValue = resolveAndSetIfAvailableMethod.Invoke(tableHolderResolver, new[] { context });

            var tableHolderProperty = setAsDispatchedHolderType.GetProperty("TableHolder");
            tableHolderProperty.SetValue(setAsDispatchedInstance, tableHolderValue);

            var partitionKeyProperty = setAsDispatchedHolderType.GetProperty("PartitionKey");
            partitionKeyProperty.SetValue(setAsDispatchedInstance, partitionKey);

            return setAsDispatchedInstance;
        }
        catch (Exception e)
        {
            throw new Exception("Unable to create a valid instance of `NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder`", e);
        }
    }

    internal class AzureTableControlMessageBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        public const string PartitionKeyStringHeaderKey = "NServiceBus.TxSession.AzureTable.PartitionKeyString";
        public const string TableInformationHeaderKey = "NServiceBus.TxSession.AzureTable.TableInformation";

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (context.Message.Headers.TryGetValue(PartitionKeyStringHeaderKey, out var partitionKeyString))
            {
                var partitionKeyInstance = CreatePartitionKeyInstance(partitionKeyString);
                context.Extensions.Set(TableEntityPartitionKeyTypeName, partitionKeyInstance);
            }

            if (context.Message.Headers.TryGetValue(TableInformationHeaderKey, out string containerName))
            {
                var tableInformationInstance = CreateTableInformationInstance(containerName);
                context.Extensions.Set(TableInformationTypeName, tableInformationInstance);
            }

            return next(context);
        }
    }
}