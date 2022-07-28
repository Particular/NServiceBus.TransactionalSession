namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Features;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Outbox;
    using NUnit.Framework;
    using ObjectBuilder;
    using Pipeline;

    public class When_outbox_entry_becomes_visible_after_tx_timeout : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void LoadAssemblies()
        {
            _ = typeof(ITransactionalSession).GetType();
            _ = typeof(CustomTestingPersistence).GetType();
        }
        [Test]
        public async Task Should_fail_to_process_control_message()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(async (_, ctx) =>
                    {
                        using var scope = ctx.Builder.CreateChildBuilder();
                        using var transactionalSession = scope.Build<ITransactionalSession>();

                        var options = new OpenSessionOptions { MaximumCommitDuration = TimeSpan.Zero };
                        await transactionalSession.Open(options);

                        await transactionalSession.Send(new SomeMessage());

                        await transactionalSession.Commit();

                        ctx.TransactionalSessionId = transactionalSession.SessionId;
                    }))
                .WithEndpoint<ReceiverEndpoint>()
                .Done(c => c.FailedMessages.Count > 0)
                .Run(TimeSpan.FromSeconds(90));

            Assert.IsFalse(context.MessageReceived, "message should never be dispatched");
            var failedMessage = context.FailedMessages.Single().Value.Single();
            // message should fail because it can't create an outbox record for the control message since the sender has already created the record and this causes a concurrency exception
            // once the failed control message retries, the outbox record should be correctly found by the storage and the contained messages will be dispatched.
            Assert.AreEqual($"Outbox message with id '{context.TransactionalSessionId}' is already present in storage.", failedMessage.Exception.Message);
            Assert.AreEqual(context.TransactionalSessionId, failedMessage.MessageId);

        }

        class Context : ScenarioContext, IInjectBuilder
        {
            public IBuilder Builder { get; set; }
            public bool MessageReceived { get; set; }
            public string TransactionalSessionId { get; set; }
        }

        class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint() =>
                EndpointSetup<DefaultServer>((c, r) =>
                {
                    c.EnableTransactionalSession();
                    c.EnableOutbox();

                    c.EnableFeature<CaptureBuilderFeature>();

                    var receiverEndpointName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ReceiverEndpoint));
                    c.ConfigureTransport().Routing().RouteToEndpoint(typeof(SomeMessage), receiverEndpointName);
                    c.Pipeline.Register(new StorageManipulationBehavior(), "configures the outbox to not see the commited values yet");
                });

            class StorageManipulationBehavior : Behavior<ITransportReceiveContext>
            {
                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    context.Extensions.Set<OutboxMessage>("TestOutboxStorage.GetResult", null); // no outbox record will be found

                    return next();
                }
            }
        }

        class ReceiverEndpoint : EndpointConfigurationBuilder
        {
            public ReceiverEndpoint() => EndpointSetup<DefaultServer>();

            class MessageHandler : IHandleMessages<SomeMessage>
            {
                public MessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;
                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        class SomeMessage : IMessage
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