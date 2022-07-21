namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using AcceptanceTesting.Customization;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    public class When_outbox_commits_after_receiving_control_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_retry_till_outbox_transaction_commited()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<SenderEndpoint>(e => e
                    .When(async (session, context) =>
                    {
                        var transactionalSession = context.ServiceProvider.GetRequiredService<ITransactionalSession>();
                        using (transactionalSession)
                        {
                            (transactionalSession as OutboxTransactionalSession).contextBag
                                .Set(CustomTestingOutboxTransaction.TransactionCommitTCSKey, context.TxCommitTcs = new TaskCompletionSource<bool>());

                            await transactionalSession.Open();
                            await transactionalSession.Send(new SomeMessage());
                            await transactionalSession.Send(new SomeMessage());
                            await transactionalSession.Send(new SomeMessage());
                            await transactionalSession.Commit();
                        }
                    }))
                .WithEndpoint<ReceiverEndpoint>()
                .Done(c => c.MessageReceiveCounter == 3)
                .Run(TimeSpan.FromSeconds(15));
        }

        class Context : ScenarioContext
        {
            public int MessageReceiveCounter = 0;
            public IServiceProvider ServiceProvider { get; set; }
            public TaskCompletionSource<bool> TxCommitTcs { get; set; }
        }

        class SenderEndpoint : EndpointConfigurationBuilder
        {
            public SenderEndpoint() =>
                EndpointSetup<TransactionSessionWithOutboxEndpoint>((c, r) =>
                {
                    c.ConfigureRouting().RouteToEndpoint(typeof(SomeMessage), typeof(ReceiverEndpoint));

                    c.Pipeline.Register(new DelayedOutboxTransactionCommitBehavior((Context)r.ScenarioContext), "delays the outbox transaction commit");

                    c.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, (Context)r.ScenarioContext));
                });


            class DelayedOutboxTransactionCommitBehavior : Behavior<ITransportReceiveContext>
            {
                readonly Context testContext;

                public DelayedOutboxTransactionCommitBehavior(Context testContext)
                {
                    this.testContext = testContext;
                }

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
            }

            class CaptureServiceProviderStartupTask : FeatureStartupTask
            {
                public CaptureServiceProviderStartupTask(IServiceProvider serviceProvider, Context context) => context.ServiceProvider = serviceProvider;

                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
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