namespace NServiceBus.TransactionalSession;

using System;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;

/// <summary>
/// Provides <see cref="ITransactionalSession"/> support for Azure Table Storage.
/// </summary>
public static class AzureTableSupport
{
    static string TableInformationTypeName = "NServiceBus.TableInformation";
    static string TableEntityPartitionKeyTypeName = "NServiceBus.TableEntityPartitionKey";
    static string SetAsDispatchedHolderTypeName = "NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder";
    internal static string TableHolderResolverAssemblyQualifiedTypeName = "NServiceBus.Persistence.AzureTable.TableHolderResolver, NServiceBus.Persistence.AzureTable";

    /// <summary>
    /// Opens a <see cref="ITransactionalSession"/> connected to a AzureTable storage.
    /// </summary>
    /// <param name="session">The session to open</param>
    /// <param name="partitionKeyHeaderName">The header key name used to determine the partition id.</param>
    /// <param name="partitionKey">The specific partition used for this session.</param>
    /// <param name="tableName"></param>
    /// <param name="options">The specific options used to open this session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns></returns>
    public static Task OpenAzureTableSession(this ITransactionalSession session, string partitionKeyHeaderName, string partitionKey, string tableName = null, OpenSessionOptions options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullAndEmpty(nameof(partitionKeyHeaderName), "Partition key header name cannot be null.");
        Guard.AgainstNullAndEmpty(nameof(partitionKey), value: "Partition key value cannot be null.");

        options ??= new OpenSessionOptions();

        options.Metadata.Add(partitionKeyHeaderName, partitionKey);

        var partitionKeyInstance = CreatePartitionKeyInstance(partitionKey);
        options.Extensions.Set(TableEntityPartitionKeyTypeName, partitionKeyInstance);

        if (tableName != null)
        {
            options.Extensions.Set(TableInformationTypeName, CreateTableInformationInstance(tableName));
        }

        var tableHolderResolver = session.PersisterSpecificOptions.Get<object>();
        options.Extensions.Set(SetAsDispatchedHolderTypeName, CreateSetAsDispatchedInstance(tableHolderResolver, partitionKeyInstance, options.Extensions));

        return session.Open(options, cancellationToken);
    }

    static object CreateTableInformationInstance(string tableName)
    {
        var tableInformationType = Type.GetType("NServiceBus.TableInformation, NServiceBus.Persistence.AzureTable, Version = 4.0.0.0, Culture = neutral, PublicKeyToken = 9fc386479f8a226c");

        var tableInformationInstance = Activator.CreateInstance(tableInformationType, new object[] { tableName });

        return tableInformationInstance;
    }

    //TODO support other partitionkey values
    static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        //TODO make lazy for caching
        var partitionKeyType = Type.GetType("NServiceBus.TableEntityPartitionKey, NServiceBus.Persistence.AzureTable, Version=4.0.0.0, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKeyString);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return partitionKeyInstance;
    }

    static object CreateSetAsDispatchedInstance(object tableHolderResolver, object partitionKey, ContextBag context)
    {
        //TODO make lazy for caching
        var setAsDispatchedType = Type.GetType("NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder, NServiceBus.Persistence.AzureTable, Version=4.0.0.0, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var setAsDispatchedInstance = Activator.CreateInstance(setAsDispatchedType);

        var resolveAndSetIfAvailableMethod = tableHolderResolver.GetType().GetMethod("ResolveAndSetIfAvailable");
        var tableHolderValue = resolveAndSetIfAvailableMethod.Invoke(tableHolderResolver, new[] { context });

        var tableHolderProperty = setAsDispatchedType.GetProperty("TableHolder");
        tableHolderProperty.SetValue(setAsDispatchedInstance, tableHolderValue);

        var partitionKeyProperty = setAsDispatchedType.GetProperty("PartitionKey");
        partitionKeyProperty.SetValue(setAsDispatchedInstance, partitionKey);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return setAsDispatchedInstance;
    }
}