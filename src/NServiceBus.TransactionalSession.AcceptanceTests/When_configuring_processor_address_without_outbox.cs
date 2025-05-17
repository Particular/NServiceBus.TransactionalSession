namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using AcceptanceTesting;
using Configuration.AdvancedExtensibility;
using NUnit.Framework;

public class When_configuring_processor_address_without_outbox : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_throw_when_processor_address_specified_without_outbox()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Scenario.Define<Context>()
                .WithEndpoint<EndpointWithProcessorAddressButNoOutbox>()
                .Done(c => false) // Will never complete normally
                .Run();
        });

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception.Message, Is.EqualTo(
            "A ProcessorEndpoint can only be specified for send-only endpoints with Outbox enabled"));
    }

    class Context : TransactionalSessionTestContext
    {
    }

    class EndpointWithProcessorAddressButNoOutbox : EndpointConfigurationBuilder
    {
        public EndpointWithProcessorAddressButNoOutbox() =>
            EndpointSetup<DefaultServer>(c =>
            {
                // Configure TransactionalSession with a processor address but don't enable Outbox
                var options = new TransactionalSessionOptions { ProcessorEndpoint = "AnotherEndpoint" };

                c.GetSettings().Get<PersistenceExtensions<CustomTestingPersistence>>()
                    .EnableTransactionalSession(options);

                // Notice: No c.EnableOutbox() call here
                c.SendOnly();
            });
    }
}