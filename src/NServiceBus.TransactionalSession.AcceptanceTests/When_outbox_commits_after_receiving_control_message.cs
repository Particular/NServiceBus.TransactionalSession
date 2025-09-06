namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
                    await using var scope = context.ServiceProvider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

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

    class Context : TransactionalSessionTestContext
    {
        public int MessageReceiveCounter;
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


        class DelayedOutboxTransactionCommitBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
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
    }

    class ReceiverEndpoint : EndpointConfigurationBuilder
    {
        public ReceiverEndpoint() => EndpointSetup<DefaultServer>();

        class MessageHandler(Context testContext) : IHandleMessages<SomeMessage>
        {
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