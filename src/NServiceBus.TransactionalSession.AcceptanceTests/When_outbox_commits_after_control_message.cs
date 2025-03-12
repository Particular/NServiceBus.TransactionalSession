namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Pipeline;
using NUnit.Framework;

public class When_outbox_commits_after_control_message : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_throw_exception_to_session_user()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SenderEndpoint>(e => e.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                try
                {
                    ctx.TransactionTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var options = new CustomTestingPersistenceOpenSessionOptions
                    {
                        CommitDelayIncrement = TimeSpan.FromSeconds(1),
                        MaximumCommitDuration = TimeSpan.FromSeconds(8),
                        TransactionCommitTaskCompletionSource = ctx.TransactionTaskCompletionSource
                    };

                    await transactionalSession.Open(options);
                    await transactionalSession.Send(new SomeMessage());
                    await transactionalSession.Commit();
                }
                catch (Exception exception)
                {
                    ctx.TransactionalSessionException = exception;
                }
            }))
            .WithEndpoint<ReceiverEndpoint>()
            .Done(c => c.TransactionalSessionException != null)
            .Run();

        Assert.That(context.MessageReceived, Is.False);

    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public IServiceProvider ServiceProvider { get; set; }
        public bool MessageReceived { get; set; }
        public Exception TransactionalSessionException { get; set; }
        public TaskCompletionSource<bool> TransactionTaskCompletionSource { get; set; }
    }

    class SenderEndpoint : EndpointConfigurationBuilder
    {
        public SenderEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint>((c, r) =>
            {
                c.Pipeline.Register(new UnblockCommitBehavior((Context)r.ScenarioContext), "unblocks the transactional session commit operation");
                c.ConfigureRouting().RouteToEndpoint(typeof(SomeMessage), typeof(ReceiverEndpoint));
            });

        class UnblockCommitBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.ContainsKey(OutboxTransactionalSession.RemainingCommitDurationHeaderName))
                {
                    context.Extensions.Set("TestOutboxStorage.StoreCallback", () =>
                    {
                        // unblock the outbox transaction from the TransactionalSession.Commit
                        // we need to wait till the TransactionalSessionDelayControlMessageBehavior gave up on retrying and therefore
                        // the outbox storage will store the current control message as a "tombstone".
                        testContext.TransactionTaskCompletionSource.TrySetResult(true);
                    });
                }

                await next();
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
                testContext.MessageReceived = true;
                return Task.CompletedTask;
            }
        }
    }

    class SomeMessage : IMessage
    {
    }
}