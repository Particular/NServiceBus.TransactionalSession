namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using AcceptanceTesting;
using Configuration.AdvancedExtensibility;
using NUnit.Framework;

public class When_using_outbox_and_configuring_a_processor_endpoint : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_throw_when_processor_address_is_specified()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Scenario.Define<Context>()
                .WithEndpoint<NonSendOnlyEndpointConfiguredToUseAProcessorEndpoint>()
                .Done(c => false) // Will never complete normally
                .Run();
        });

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception.Message,
            Is.EqualTo("A ProcessorEndpoint can only be specified for send-only endpoints"));
    }

    class Context : TransactionalSessionTestContext
    {
    }

    class NonSendOnlyEndpointConfiguredToUseAProcessorEndpoint : EndpointConfigurationBuilder
    {
        public NonSendOnlyEndpointConfiguredToUseAProcessorEndpoint() =>
            EndpointSetup<DefaultServer>(c =>
            {
                var options = new TransactionalSessionOptions { ProcessorEndpoint = "AnotherEndpoint" };
                c.GetSettings().Get<PersistenceExtensions<CustomTestingPersistence>>().EnableTransactionalSession(options);

                c.EnableOutbox();
                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            });
    }
}