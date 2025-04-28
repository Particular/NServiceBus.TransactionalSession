namespace NServiceBus.AcceptanceTesting;

using System;
using Extensibility;
using Features;
using Microsoft.Extensions.DependencyInjection;
using Outbox;
using TransactionalSession;

sealed class CustomTestingOutboxPersistence : Feature
{
    public CustomTestingOutboxPersistence()
    {
        DependsOn<Outbox>();
        Defaults(s => s.EnableFeatureByDefault<CustomTestingSynchronizedStorageFeature>());
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        IOutboxStorage outboxStorage = null;

        if (context.Settings.TryGet<TransactionalSessionOptions>(out var transactionalSessionOptions))
        {
            if (transactionalSessionOptions.GetExtensions().TryGet<IOutboxStorage>(SharedStorageKey, out var sharedOutboxStorage))
            {
                outboxStorage = sharedOutboxStorage;
            }
        }

        if (outboxStorage is null)
        {
            context.Services.AddSingleton<IOutboxStorage, CustomTestingOutboxStorage>();
        }
        else
        {
            context.Services.AddSingleton(typeof(IOutboxStorage), outboxStorage);
        }
    }

    public const string SharedStorageKey = "CustomTestingOutboxPersistence.SharedStorage";
}

static class CustomTestingOutboxPersistenceConfigurationExtensions
{
    public static void SharedOutboxStorage(this TransactionalSessionOptions options, IOutboxStorage outboxStorage)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(outboxStorage);

        options.GetExtensions().Set(CustomTestingOutboxPersistence.SharedStorageKey, outboxStorage);
    }
}