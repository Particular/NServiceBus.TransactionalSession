﻿namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ObjectBuilder;

    public class When_not_using_outbox : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void LoadAssemblies()
        {
            _ = typeof(ITransactionalSession).GetType();
            _ = typeof(CustomTestingPersistence).GetType();
        }

        [Test]
        public async Task Should_send_messages_on_transactional_session_commit()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.Builder.CreateChildBuilder();
                    using var transactionalSession = scope.Build<ITransactionalSession>();

                    await transactionalSession.Open();

                    await transactionalSession.SendLocal(new SampleMessage());

                    await transactionalSession.Commit();
                }))
                .Done(c => c.MessageReceived)
                .Run()
                ;
        }

        [Test]
        public async Task Should_not_send_messages_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (messageSession, ctx) =>
                {
                    using (var scope = ctx.Builder.CreateChildBuilder())
                    using (var transactionalSession = scope.Build<ITransactionalSession>())
                    {
                        await transactionalSession.Open();

                        await transactionalSession.SendLocal(new SampleMessage());
                    }

                    //Send immediately dispatched message to finish the test
                    await messageSession.SendLocal(new CompleteTestMessage());
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run()
                ;

            Assert.True(result.CompleteMessageReceived);
            Assert.False(result.MessageReceived);
        }

        class Context : ScenarioContext, IInjectBuilder
        {
            public bool MessageReceived { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public IBuilder Builder { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>((c, r) =>
                {
                    c.EnableTransactionalSession();
                    c.EnableFeature<CaptureBuilderFeature>();
                });
            }

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

        public class CaptureBuilderFeature : Feature
        {
            protected override void Setup(FeatureConfigurationContext context)
            {
                var scenarioContext = context.Settings.Get<ScenarioContext>();
                context.RegisterStartupTask(builder => new CaptureServiceProviderStartupTask(builder, scenarioContext));
            }

            class CaptureServiceProviderStartupTask : FeatureStartupTask
            {
                public CaptureServiceProviderStartupTask(IBuilder builder, ScenarioContext context)
                {
                    if (context is IInjectBuilder c)
                    {
                        c.Builder = builder;
                    }
                }

                protected override Task OnStart(IMessageSession session) => Task.CompletedTask;

                protected override Task OnStop(IMessageSession session) => Task.CompletedTask;
            }
        }

        public interface IInjectBuilder
        {
            IBuilder Builder { get; set; }
        }
    }
}