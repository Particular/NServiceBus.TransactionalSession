namespace NServiceBus.AcceptanceTesting;

using System;
using Configuration.AdvancedExtensibility;
using Outbox;

public static class CustomTestingOutboxExtensions
{
    public static void EndpointName(this OutboxSettings configuration, string endpointName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);

        configuration.GetSettings().Set(CustomTestingOutboxPersistence.EndpointNameKey, endpointName);
    }
}