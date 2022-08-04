namespace NServiceBus.TransactionalSession;

using System;
using Extensibility;

static class AzureTableSupport
{
    public static string TableEntityPartitionKeyTypeName = "NServiceBus.TableEntityPartitionKey";
    public static string SetAsDispatchedHolderTypeName = "NServiceBus.Persistence.AzureTable.SetAsDispatchedHolder";
    public static string TableHolderResolverAssemblyQualifiedTypeName = "NServiceBus.Persistence.AzureTable.TableHolderResolver, NServiceBus.Persistence.AzureTable";

    //TODO support other partitionkey values
    public static object CreatePartitionKeyInstance(string partitionKeyString)
    {
        //TODO make lazy for caching
        var partitionKeyType = Type.GetType("NServiceBus.TableEntityPartitionKey, NServiceBus.Persistence.AzureTable, Version=4.0.0.0, Culture=neutral, PublicKeyToken=9fc386479f8a226c");
        var partitionKeyInstance = Activator.CreateInstance(partitionKeyType, partitionKeyString);

        //TODO throw exception if anything goes wrong because that would indicate a critical error
        return partitionKeyInstance;
    }

    public static object CreateSetAsDispatchedInstance(object tableHolderResolver, object partitionKey, ContextBag context)
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