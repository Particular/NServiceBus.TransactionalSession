namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_using_outbox : NServiceBusAcceptanceTest
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

                    await transactionalSession.SendLocal(new SampleMessage(), CancellationToken.None);

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();
        }

        [Test]
        public async Task Should_not_send_messages_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
                {
                    using (var scope = ctx.ServiceProvider.CreateScope())
                    using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                    {
                        await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                        await transactionalSession.SendLocal(new SampleMessage());
                    }

                    //Send immediately dispatched message to finish the test
                    await statelessSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();

            Assert.True(result.CompleteMessageReceived);
            Assert.That(result.MessageReceived, Is.False);
        }

        [Test]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                    var sendOptions = new SendOptions();
                    sendOptions.RequireImmediateDispatch();
                    sendOptions.RouteToThisEndpoint();
                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);
                }))
                .Done(c => c.MessageReceived)
                .Run()
                ;

            Assert.True(result.MessageReceived);
        }

        [Test]
        public async Task Should_make_it_possible_float_ambient_transactions()
        {
            var result = await Scenario.Define<Context>()
                    .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                    {
                        using var scope = ctx.ServiceProvider.CreateScope();
                        using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                        await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions
                        {
                            UseTransactionScope = true
                        });

                        ctx.AmbientTransactionFoundBeforeAwait = Transaction.Current != null;

                        await Task.Yield();

                        ctx.AmbientTransactionFoundAfterAwait = Transaction.Current != null;
                    }))
                    .Done(c => c.EndpointsStarted)
                    .Run();

            Assert.True(result.AmbientTransactionFoundBeforeAwait, "The ambient transaction was not visible before the await");
            Assert.True(result.AmbientTransactionFoundAfterAwait, "The ambient transaction was not visible after the await");
        }

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool AmbientTransactionFoundBeforeAwait { get; set; }
            public bool AmbientTransactionFoundAfterAwait { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public bool MessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup<TransactionSessionWithOutboxEndpoint>();

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
                public CompleteTestMessageHandler(Context context) => testContext = context;

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