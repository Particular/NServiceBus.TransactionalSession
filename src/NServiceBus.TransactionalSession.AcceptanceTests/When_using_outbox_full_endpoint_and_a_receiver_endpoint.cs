namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;
using Pipeline;

public class When_using_outbox_full_endpoint_and_a_receiver_endpoint : NServiceBusAcceptanceTest
{
    [Test, Ignore("Not implemented yet")]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<EndpointWithTransactionalSession>(s => s.When(async (_, ctx) =>
            {

                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                await transactionalSession.Send(new SampleMessage(), CancellationToken.None);

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .WithEndpoint<AnotherEndpoint>()
            .WithEndpoint<ProcessorEndpoint>()
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.ControlMessageReceived, Is.True);
        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }

        public bool ControlMessageReceived { get; set; }
    }

    class EndpointWithTransactionalSession : EndpointConfigurationBuilder
    {
        public EndpointWithTransactionalSession() => EndpointSetup<DefaultServerWithServiceProviderCapturing>(c =>
        {
            var options = new TransactionalSessionOptions { ProcessorAddress = Conventions.EndpointNamingConvention.Invoke(typeof(ProcessorEndpoint)) };
            var persistence = c.UsePersistence<CustomTestingPersistence>();
            persistence.EnableTransactionalSession(options);

            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

            c.EnableOutbox();

            c.ConfigureRouting().RouteToEndpoint(typeof(SampleMessage), typeof(AnotherEndpoint));

        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServerWithServiceProviderCapturing>(c =>
        {
            c.UsePersistence<CustomTestingPersistence>();

            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

            c.EnableOutbox();

        });

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
        public ProcessorEndpoint() => EndpointSetup<DefaultServer>(c =>
            {
                c.Pipeline.Register(typeof(DiscoverControlMessagesBehavior), "Discovers control messages");
                c.EnableOutbox();
                c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

                c.UsePersistence<CustomTestingPersistence>()
                    .EnableTransactionalSession();
            }
        );

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