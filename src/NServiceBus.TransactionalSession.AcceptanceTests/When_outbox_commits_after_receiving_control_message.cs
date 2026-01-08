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
    public async Task Should_retry_till_outbox_transaction_committed() =>
        await Scenario.Define<Context>()
            .WithEndpoint<SenderEndpoint>(e => e
                .When(async (_, context) =>
                {
                    await using var scope = context.ServiceProvider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    var options = new CustomTestingPersistenceOpenSessionOptions
                    {
                        TransactionCommitTaskCompletionSource = context.TransactionTaskCompletionSource
                    };

                    await transactionalSession.Open(options);
                    await transactionalSession.Send(new SomeMessage());
                    await transactionalSession.Send(new SomeMessage());
                    await transactionalSession.Send(new SomeMessage());

                    await transactionalSession.Commit();
                }))
            .WithEndpoint<ReceiverEndpoint>()
            .Run();

    class Context : TransactionalSessionTestContext
    {
        int messageReceiveCounter;

        public void MaybeCompleted() => MarkAsCompleted(Interlocked.Increment(ref messageReceiveCounter) == 3);
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
                testContext.TransactionTaskCompletionSource.TrySetResult(true);
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
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class SomeMessage : IMessage;
}