namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Pipeline;

    public class When_using_outbox : NServiceBusAcceptanceTest
    {
        static string PartitionKeyHeaderName = "Tests.PartitionKey";
        static string PartitionKeyValue = "SomePartition";

        [Test]
        public async Task Should_send_messages_on_transactional_session_commit()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue);

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    sendOptions.RouteToThisEndpoint();

                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);

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
                        await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue);

                        var sendOptions = new SendOptions();
                        sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                        sendOptions.RouteToThisEndpoint();

                        await transactionalSession.Send(new SampleMessage(), sendOptions);
                    }

                    var competeMessageSendOptions = new SendOptions();
                    competeMessageSendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    competeMessageSendOptions.RouteToThisEndpoint();

                    //Send immediately dispatched message to finish the test
                    await statelessSession.Send(new CompleteTestMessage(), competeMessageSendOptions);
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

                    await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue);

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
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
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup<TransactionSessionWithOutboxEndpoint>(c =>
                {
                    c.Pipeline.Register(sp => new PartitionKeyProviderBehavior(), "Extract partition key value from message header.");
                });

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

        class PartitionKeyProviderBehavior : Behavior<ITransportReceiveContext>
        {
            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.TryGetValue(PartitionKeyHeaderName, out var partitionKeyValue))
                {
                    context.Extensions.Set(new TableEntityPartitionKey(partitionKeyValue));
                }

                return next();
            }
        }
    }
}