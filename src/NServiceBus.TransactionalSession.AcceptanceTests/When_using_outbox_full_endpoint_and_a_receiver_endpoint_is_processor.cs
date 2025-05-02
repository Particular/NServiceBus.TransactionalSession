namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using NUnit.Framework;
using Outbox;
using Pipeline;

public class When_using_outbox_full_endpoint_and_a_receiver_endpoint_is_processor : NServiceBusAcceptanceTest
{
    [Test()]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<FullEndpointWithTransactionalSession>(s => s.When(async (_, ctx) =>
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
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.ControlMessageReceived, Is.True);
        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public CustomTestingOutboxStorage SharedOutboxStorage { get; } = new();

        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public bool ControlMessageReceived { get; set; }
    }

    class FullEndpointWithTransactionalSession : EndpointConfigurationBuilder
    {
        public FullEndpointWithTransactionalSession() => EndpointSetup<DefaultServer>((c, runDescriptor) =>
        {
            var options = new TransactionalSessionOptions { ProcessorAddress = Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)) };

            options.SharedOutboxStorage(((Context)runDescriptor.ScenarioContext).SharedOutboxStorage);

            var persistence = c.UsePersistence<CustomTestingPersistence>();

            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder, IDoNotCaptureServiceProvider
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServer>((c, runDescriptor) =>
        {
            c.Pipeline.Register(typeof(DiscoverControlMessagesBehavior), "Discovers control messages");

            var persistence = c.UsePersistence<CustomTestingPersistence>();

            var options = new TransactionalSessionOptions();

            options.SharedOutboxStorage(((Context)runDescriptor.ScenarioContext).SharedOutboxStorage);

            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
        });

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }

        class DiscoverControlMessagesBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.ContainsKey(OutboxTransactionalSession.CommitDelayIncrementHeaderName))
                {
                    testContext.ControlMessageReceived = true;
                }

                await next();
            }
        }
    }

    class SampleMessage : ICommand
    {
    }
}