namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_using_transactional_session_in_the_pipeline : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_work()
        {
            var scenarioContext = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                    await transactionalSession.SendLocal(new KickOffMessage(), CancellationToken.None);

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.TransactionalSessionMessageReceived && c.MessageHandlerContextMessageReceived)
                .Run();

            Assert.That(scenarioContext.TransactionalSessionMessageReceived, Is.True);
            Assert.That(scenarioContext.MessageHandlerContextMessageReceived, Is.True);
        }

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool TransactionalSessionMessageReceived { get; set; }
            public bool MessageHandlerContextMessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup<TransactionSessionDefaultServer>();

            class KickOffHandler : IHandleMessages<KickOffMessage>
            {
                public KickOffHandler(ITransactionalSession transactionalSession) => this.transactionalSession = transactionalSession;

                public async Task Handle(KickOffMessage message, IMessageHandlerContext context)
                {
                    await context.SendLocal(new MessageHandlerContextMessage());
                    await transactionalSession.SendLocal(new TransactionalSessionMessage());
                }

                readonly ITransactionalSession transactionalSession;
            }

            class TransactionSessionMessageHandler : IHandleMessages<TransactionalSessionMessage>
            {
                public TransactionSessionMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(TransactionalSessionMessage message, IMessageHandlerContext context)
                {
                    testContext.TransactionalSessionMessageReceived = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class MessageHandlerContextMessageMessageHandler : IHandleMessages<MessageHandlerContextMessage>
            {
                public MessageHandlerContextMessageMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MessageHandlerContextMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageHandlerContextMessageReceived = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class KickOffMessage : ICommand
        {
        }

        class TransactionalSessionMessage : ICommand
        {
        }

        class MessageHandlerContextMessage : ICommand
        {
        }
    }
}