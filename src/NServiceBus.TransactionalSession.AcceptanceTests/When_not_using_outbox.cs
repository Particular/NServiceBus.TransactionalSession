namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using NUnit.Framework;

public class When_not_using_outbox : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                await transactionalSession.SendLocal(new SampleMessage(), ct);

                await transactionalSession.Commit(ct);
            }, afterStart: true))
            .Run();

        Assert.That(result.MessageReceived, Is.True);
    }

    [Test]
    public async Task Should_not_send_messages_if_session_is_not_committed()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s =>
            {
                s.ServiceResolve(async (provider, ctx, ct) =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                    await transactionalSession.SendLocal(new SampleMessage(), ct);
                }, afterStart: true);
                s.When(async (messageSession, ctx) =>
                {
                    await messageSession.SendLocal(new CompleteTestMessage());
                });
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
        }
    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
        public bool CompleteMessageReceived { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() => EndpointSetup<TransactionSessionDefaultServer>();

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class SampleMessage : ICommand;

    class CompleteTestMessage : ICommand;
}