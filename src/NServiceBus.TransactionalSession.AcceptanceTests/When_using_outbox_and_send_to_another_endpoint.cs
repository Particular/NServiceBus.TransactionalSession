namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;

// This test verifies that a regular full endpoint (non send only) can successfully deliver to another endpoint when using the transactional session
public class When_using_outbox_and_send_to_another_endpoint : NServiceBusAcceptanceTest
{
    [Test()]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<FullEndpointWithTransactionalSession>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                var options = new SendOptions();

                options.SetDestination(Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)));

                await transactionalSession.Send(new SampleMessage(), options, ct);

                await transactionalSession.Commit(ct);
            }, afterStart: true))
            .WithEndpoint<AnotherEndpoint>()
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
    }

    class FullEndpointWithTransactionalSession : EndpointConfigurationBuilder
    {
        public FullEndpointWithTransactionalSession() => EndpointSetup<TransactionSessionWithOutboxEndpoint>();
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServer>();

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }
    class SampleMessage : ICommand;
}