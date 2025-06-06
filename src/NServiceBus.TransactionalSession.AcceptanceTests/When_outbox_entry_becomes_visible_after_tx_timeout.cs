﻿namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;
using Outbox;
using Pipeline;

public class When_outbox_entry_becomes_visible_after_tx_timeout : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_fail_to_process_control_message()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SenderEndpoint>(e => e
                .DoNotFailOnErrorMessages()
                .When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    var options = new CustomTestingPersistenceOpenSessionOptions { MaximumCommitDuration = TimeSpan.Zero };
                    await transactionalSession.Open(options);

                    await transactionalSession.Send(new SomeMessage());

                    await transactionalSession.Commit();

                    ctx.TransactionalSessionId = transactionalSession.SessionId;
                }))
            .WithEndpoint<ReceiverEndpoint>()
            .Done(c => c.FailedMessages.Count > 0)
            .Run(TimeSpan.FromSeconds(90));

        Assert.That(context.MessageReceived, Is.False, "message should never be dispatched");
        var failedMessage = context.FailedMessages.Single().Value.Single();
        Assert.Multiple(() =>
        {
            // message should fail because it can't create an outbox record for the control message since the sender has already created the record and this causes a concurrency exception
            // once the failed control message retries, the outbox record should be correctly found by the storage and the contained messages will be dispatched.
            Assert.That(failedMessage.Exception.Message.Contains($"Outbox message with id '{context.TransactionalSessionId}' "), Is.True);
            Assert.That(failedMessage.MessageId, Is.EqualTo(context.TransactionalSessionId));
        });

    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
        public string TransactionalSessionId { get; set; }
    }

    class SenderEndpoint : EndpointConfigurationBuilder
    {
        public SenderEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint>((c, r) =>
            {
                c.ConfigureRouting().RouteToEndpoint(typeof(SomeMessage), typeof(ReceiverEndpoint));
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