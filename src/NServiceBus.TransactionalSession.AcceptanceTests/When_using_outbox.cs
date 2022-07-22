namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using Features;
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
                    await transactionalSession.Open();

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
                        await transactionalSession.Open();

                        await transactionalSession.SendLocal(new SampleMessage());
                    }

                    //Send immediately dispatched message to finish the test
                    await statelessSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();

            Assert.True(result.CompleteMessageReceived);
            Assert.False(result.MessageReceived);
        }

        [Test]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open();

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
        public async Task Should_fail_commit_and_not_send_messages_when_timeout_elapsed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open();

                    await transactionalSession.SendLocal(new SampleMessage());

                    transactionalSession.SynchronizedStorageSession.DelayCommit(TimeSpan.FromSeconds(30));

                    Assert.ThrowsAsync<Exception>(() => transactionalSession.Commit());

                    ctx.CompleteMessageReceived = true;
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run()
                ;

            Assert.False(result.MessageReceived);
        }

        //Should commit the DB transaction if message send fails

        class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>((c, r) =>
                {
                    c.EnableOutbox();
                    c.EnableTransactionalSession();
                    c.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, r.ScenarioContext as Context));
                });
            }

            class SampleHandler : IHandleMessages<SampleMessage>
            {
                public SampleHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    this.testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
            {
                public CompleteTestMessageHandler(Context context) => this.testContext = context;

                public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
                {
                    testContext.CompleteMessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class CaptureServiceProviderStartupTask : FeatureStartupTask
            {

                public CaptureServiceProviderStartupTask(IServiceProvider serviceProvider, Context context)
                {
                    context.ServiceProvider = serviceProvider;
                }

                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
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