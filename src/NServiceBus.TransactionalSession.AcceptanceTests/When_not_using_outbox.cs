namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_not_using_outbox : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_send_messages_on_transactional_session_commit()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                    await transactionalSession.SendLocal(new SampleMessage());

                    await transactionalSession.Commit();
                }))
                .Done(c => c.MessageReceived)
                .Run();
        }

        [Test]
        public async Task Should_not_send_messages_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (messageSession, ctx) =>
                {
                    using (var scope = ctx.ServiceProvider.CreateScope())
                    using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                    {
                        await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                        await transactionalSession.SendLocal(new SampleMessage());
                    }

                    //Send immediately dispatched message to finish the test
                    await messageSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();

            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
        }

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool MessageReceived { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() => EndpointSetup<TransactionSessionDefaultServer>();

            class SampleHandler : IHandleMessages<SampleMessage>
            {
                public SampleHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
            {

                public CompleteTestMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
                {
                    testContext.CompleteMessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class SampleMessage : ICommand
        {
        }

        class CompleteTestMessage : ICommand
        {
        }
    }
}