namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;
using Pipeline;

public class When_using_outbox_send_only : NServiceBusAcceptanceTest
{
    [Test()]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SendOnlyEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                var options = new SendOptions();

                options.SetDestination(Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)));

                await transactionalSession.Send(new SampleMessage(), options);

                await transactionalSession.Commit(CancellationToken.None);
            }))
            .WithEndpoint<AnotherEndpoint>()
            .WithEndpoint<ProcessorEndpoint>()
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    [Test]
    public void Should_throw_when_processor_address_not_specified()
    {
        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Scenario.Define<Context>()
                .WithEndpoint<SendOnlyEndpointWithoutProcessor>()
                .Done(c => c.MessageReceived)
                .Run();
        });

        Assert.That(exception?.Message, Is.EqualTo("A configured ProcessorAddress is required when using the transactional session and the outbox with send-only endpoints"));
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public CustomTestingOutboxStorage SharedOutboxStorage { get; } = new();

        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }
    }

    class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() => EndpointSetup<DefaultServer>((c, runDescriptor) =>
        {
            var options = new TransactionalSessionOptions { ProcessorAddress = Conventions.EndpointNamingConvention.Invoke(typeof(ProcessorEndpoint)) };

            var persistence = c.UsePersistence<CustomTestingPersistence>();

            options.SharedOutboxStorage(((Context)runDescriptor.ScenarioContext).SharedOutboxStorage);

            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class SendOnlyEndpointWithoutProcessor : EndpointConfigurationBuilder
    {
        public SendOnlyEndpointWithoutProcessor() => EndpointSetup<DefaultServer>(c =>
        {
            var persistence = c.UsePersistence<CustomTestingPersistence>();

            // Deliberately not passing a ProcessorAddress via TransactionalSessionOptions
            persistence.EnableTransactionalSession();

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder, IDoNotCaptureServiceProvider
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServer>();

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    class ProcessorEndpoint : EndpointConfigurationBuilder
    {
        public ProcessorEndpoint() => EndpointSetup<DefaultServer>((c, runDescriptor) =>
            {
                c.EnableOutbox();
                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

                var persistence = c.UsePersistence<CustomTestingPersistence>();

                var options = new TransactionalSessionOptions();

                options.SharedOutboxStorage(((Context)runDescriptor.ScenarioContext).SharedOutboxStorage);

                persistence.EnableTransactionalSession(options);
            }
        );
    }

    class SampleMessage : ICommand
    {
    }
}