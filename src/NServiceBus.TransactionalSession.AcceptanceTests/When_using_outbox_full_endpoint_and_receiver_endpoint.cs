namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;
using Pipeline;

public class When_using_outbox_full_endpoint_and_receiver_endpoint : NServiceBusAcceptanceTest
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

        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }
    }

    class FullEndpointWithTransactionalSession : EndpointConfigurationBuilder
    {
        public FullEndpointWithTransactionalSession() => EndpointSetup<DefaultServerWithServiceProviderCapturing>(c =>
        {
            var persistence = c.UsePersistence<CustomTestingPersistence>();
            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            persistence.EnableTransactionalSession();

            c.EnableOutbox();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
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
    class SampleMessage : ICommand
    {
    }
}