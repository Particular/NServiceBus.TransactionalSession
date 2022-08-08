namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NUnit.Framework;
    using NUnit.Framework.Internal;

    public class When_using_outbox : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_send_messages_on_transactional_session_commit()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.OpenRavenDBSession();

                    await transactionalSession.SendLocal(new SampleMessage(), CancellationToken.None);

                    var ravenSession = transactionalSession.SynchronizedStorageSession.RavenSession();
                    var document = new TestDocument { Id = ctx.SessionId = transactionalSession.SessionId };
                    await ravenSession.StoreAsync(document);

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            var documents = RavenSetup.DocumentStore.OpenSession(RavenSetup.DefaultDatabaseName)
                .Query<TestDocument>()
                .Where(d => d.Id == context.SessionId);
            Assert.AreEqual(1, documents.Count());
        }

        [Test]
        public async Task Should_not_send_messages_if_session_is_not_committed()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
                {
                    using (var scope = ctx.ServiceProvider.CreateScope())
                    using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                    {
                        await transactionalSession.OpenRavenDBSession();

                        var ravenSession = transactionalSession.SynchronizedStorageSession.RavenSession();
                        var document = new TestDocument { Id = ctx.SessionId = transactionalSession.SessionId };
                        await ravenSession.StoreAsync(document);

                        await transactionalSession.SendLocal(new SampleMessage());
                    }

                    //Send immediately dispatched message to finish the test
                    await statelessSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();

            Assert.True(context.CompleteMessageReceived);
            Assert.False(context.MessageReceived);

            var documents = RavenSetup.DocumentStore.OpenSession(RavenSetup.DefaultDatabaseName)
                .Query<TestDocument>()
                .Where(d => d.Id == context.SessionId);
            var d = documents.FirstOrDefault();
            Assert.IsEmpty(documents);
        }

        [Test]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.OpenRavenDBSession();

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

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool MessageReceived { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
            public string SessionId { get; set; }
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

        public class TestDocument
        {
            public string Id { get; set; }
        }
    }
}