namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Infrastructure;
    using NUnit.Framework;
    using ObjectBuilder;
    using Pipeline;

    public class When_outbox_commits_after_receiving_control_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_retry_till_outbox_transaction_committed()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(e => e
                    .When(async (_, context) =>
                    {
                        using var scope = context.Builder.CreateChildBuilder();
                        using var transactionalSession = scope.Build<ITransactionalSession>();

                        var options = new CustomTestingPersistenceOpenSessionOptions
                        {
                            TransactionCommitTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
                        };
                        context.TxCommitTcs = options.TransactionCommitTaskCompletionSource;

                        await transactionalSession.Open(options);
                        await transactionalSession.Send(new SomeMessage());
                        await transactionalSession.Send(new SomeMessage());
                        await transactionalSession.Send(new SomeMessage());

                        await transactionalSession.Commit();
                    }))
                .WithEndpoint<ReceiverEndpoint>()
                .Done(c => c.MessageReceiveCounter == 3)
                .Run(TimeSpan.FromSeconds(15));
        }

        class Context : ScenarioContext, IInjectBuilder
        {
            public int MessageReceiveCounter = 0;
            public IBuilder Builder { get; set; }
            public TaskCompletionSource<bool> TxCommitTcs { get; set; }
        }

        class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint() =>
                EndpointSetup<TransactionSessionWithOutboxEndpoint>((c, r) =>
                {
                    c.ConfigureRouting().RouteToEndpoint(typeof(SomeMessage), typeof(ReceiverEndpoint));

                    c.Pipeline.Register(new DelayedOutboxTransactionCommitBehavior((Context)r.ScenarioContext), "delays the outbox transaction commit");
                });


            class DelayedOutboxTransactionCommitBehavior : Behavior<ITransportReceiveContext>
            {
                public DelayedOutboxTransactionCommitBehavior(Context testContext) => this.testContext = testContext;

                public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    try
                    {
                        await next();
                    }
                    catch (ConsumeMessageException)
                    {
                        // we don't know the order of behaviors
                    }

                    // unblock transaction once the receive pipeline "completed" once
                    testContext.TxCommitTcs.TrySetResult(true);
                }

                readonly Context testContext;
            }
        }

        class ReceiverEndpoint : EndpointConfigurationBuilder
        {
            public ReceiverEndpoint() => EndpointSetup<DefaultServer>();

            class MessageHandler : IHandleMessages<SomeMessage>
            {
                Context testContext;

                public MessageHandler(Context testContext) => this.testContext = testContext;


                public Task Handle(SomeMessage message, IMessageHandlerContext context)
                {
                    Interlocked.Increment(ref testContext.MessageReceiveCounter);
                    return Task.CompletedTask;
                }
            }
        }

        class SomeMessage : IMessage
        {
        }
    }
}