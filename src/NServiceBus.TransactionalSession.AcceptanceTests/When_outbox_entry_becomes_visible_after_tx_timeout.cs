namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Pipeline;

public class When_outbox_entry_becomes_visible_after_tx_timeout : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_fail_to_process_control_message()
    {
        Context context = null;
        var exception = Assert.CatchAsync(async () =>
            await Scenario.Define<Context>(ctx => context = ctx)
                .WithEndpoint<SenderEndpoint>(e => e
                    .When(async (_, ctx) =>
                    {
                        await using var scope = ctx.ServiceProvider.CreateAsyncScope();
                        await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                        var options = new CustomTestingPersistenceOpenSessionOptions { MaximumCommitDuration = TimeSpan.Zero };
                        await transactionalSession.Open(options);

                        ctx.TransactionalSessionId = transactionalSession.SessionId;

                        await transactionalSession.Send(new SomeMessage());

                        await transactionalSession.Commit();
                    }))
                .WithEndpoint<ReceiverEndpoint>()
                .Run());

        Assert.That(exception, Is.InstanceOf<MessageFailedException>().And.Not.InstanceOf<InvalidOperationException>().With.Message.Not.Contain("should not be processed"), "message should never be dispatched");
        var failedMessage = (exception as MessageFailedException)?.FailedMessage;
        using (Assert.EnterMultipleScope())
        {
            // message should fail because it can't create an outbox record for the control message since the sender has already created the record and this causes a concurrency exception
            // once the failed control message retries, the outbox record should be correctly found by the storage and the contained messages will be dispatched.
            Assert.That(failedMessage, Is.Not.Null);
            Assert.That(failedMessage.Exception.Message, Does.Contain($"Outbox message with id '{context.TransactionalSessionId}' "));
            Assert.That(failedMessage.MessageId, Is.EqualTo(context.TransactionalSessionId));
        }

    }

    class Context : TransactionalSessionTestContext
    {
        public string TransactionalSessionId { get; set; }
    }

    class SenderEndpoint : EndpointConfigurationBuilder
    {
        public SenderEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint>((c, r) =>
            {
                c.ConfigureRouting().RouteToEndpoint(typeof(SomeMessage), typeof(ReceiverEndpoint));
                c.Pipeline.Register(new StorageManipulationBehavior(), "configures the outbox to not see the committed values yet");
            });

        class StorageManipulationBehavior : Behavior<ITransportReceiveContext>
        {
            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                context.Extensions.Set(new CustomTestingOutboxStorageResult()); // no outbox record will be found

                return next();
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
                testContext.MarkAsFailed(new InvalidOperationException("message should not be processed"));
                return Task.CompletedTask;
            }
        }
    }

    class SomeMessage : IMessage;
}