namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_using_transactional_session_in_the_pipeline_with_multiple_handlers : NServiceBusAcceptanceTest
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
                .Done(c => c.TransactionalSessionMessage1Received && c.MessageHandlerContextMessage1Received && c.TransactionalSessionMessage2Received && c.MessageHandlerContextMessage2Received)
                .Run();

            Assert.That(scenarioContext.TransactionalSessionMessage1Received, Is.True);
            Assert.That(scenarioContext.TransactionalSessionMessage2Received, Is.True);
            Assert.That(scenarioContext.MessageHandlerContextMessage1Received, Is.True);
            Assert.That(scenarioContext.MessageHandlerContextMessage2Received, Is.True);
        }

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool TransactionalSessionMessage1Received { get; set; }
            public bool TransactionalSessionMessage2Received { get; set; }
            public bool MessageHandlerContextMessage1Received { get; set; }
            public bool MessageHandlerContextMessage2Received { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup<TransactionSessionDefaultServer>();

            class KickOffHandler1 : IHandleMessages<KickOffMessage>
            {
                public KickOffHandler1(ITransactionalSession transactionalSession) => this.transactionalSession = transactionalSession;

                public async Task Handle(KickOffMessage message, IMessageHandlerContext context)
                {
                    await context.SendLocal(new MessageHandlerContextMessage1());
                    await transactionalSession.SendLocal(new TransactionalSessionMessage1());
                }

                readonly ITransactionalSession transactionalSession;
            }

            class KickOffHandler2 : IHandleMessages<KickOffMessage>
            {
                public KickOffHandler2(ITransactionalSession transactionalSession) => this.transactionalSession = transactionalSession;

                public async Task Handle(KickOffMessage message, IMessageHandlerContext context)
                {
                    await context.SendLocal(new MessageHandlerContextMessage2());
                    await transactionalSession.SendLocal(new TransactionalSessionMessage2());
                }

                readonly ITransactionalSession transactionalSession;
            }

            class TransactionSessionMessageHandler : IHandleMessages<TransactionalSessionMessage1>, IHandleMessages<TransactionalSessionMessage2>
            {
                public TransactionSessionMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(TransactionalSessionMessage1 message, IMessageHandlerContext context)
                {
                    testContext.TransactionalSessionMessage1Received = true;
                    return Task.CompletedTask;
                }

                public Task Handle(TransactionalSessionMessage2 message, IMessageHandlerContext context)
                {
                    testContext.TransactionalSessionMessage2Received = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class MessageHandlerContextMessageMessageHandler : IHandleMessages<MessageHandlerContextMessage1>, IHandleMessages<MessageHandlerContextMessage2>
            {
                public MessageHandlerContextMessageMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MessageHandlerContextMessage1 message, IMessageHandlerContext context)
                {
                    testContext.MessageHandlerContextMessage1Received = true;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageHandlerContextMessage2 message, IMessageHandlerContext context)
                {
                    testContext.MessageHandlerContextMessage2Received = true;
                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class KickOffMessage : ICommand
        {
        }

        class TransactionalSessionMessage1 : ICommand
        {
        }

        class TransactionalSessionMessage2 : ICommand
        {
        }

        class MessageHandlerContextMessage1 : ICommand
        {
        }

        class MessageHandlerContextMessage2 : ICommand
        {
        }
    }
}