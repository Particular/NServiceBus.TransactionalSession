namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using NUnit.Framework;
using Pipeline;
using AcceptanceTesting.Customization;

public class When_using_outbox_and_audit_enabled : NServiceBusAcceptanceTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task Should_not_audit_the_control_message(bool commitHappensAfterControlMessage)
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                var options = new CustomTestingPersistenceOpenSessionOptions { TransactionCommitTaskCompletionSource = ctx.TransactionCommitTaskCompletionSource };

                if (!commitHappensAfterControlMessage)
                {
                    options.TransactionCommitTaskCompletionSource.SetResult(true);
                }

                await transactionalSession.Open(options);

                await transactionalSession.SendLocal(new SampleMessage());

                await transactionalSession.Commit();
            }))
            .WithEndpoint<AuditSpyEndpoint>()
            .Done(c => c.TestComplete)
            .Run();

        Assert.That(context.SampleMessageAudited, Is.True);
        Assert.That(context.ControlMessageWasAudited, Is.False);
    }

    class Context : TransactionalSessionTestContext
    {
        public bool SampleMessageAudited { get; set; }
        public bool ControlMessageWasAudited { get; set; }
        public bool TestComplete { get; set; }

        public TaskCompletionSource<bool> TransactionCommitTaskCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint, Context>(
                (c, testContext) =>
                {
                    c.AuditProcessedMessagesTo<AuditSpyEndpoint>();
                    c.Pipeline.Register(new DelayedOutboxTransactionCommitBehavior(testContext), "delays the outbox transaction commit");
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
                testContext.TransactionCommitTaskCompletionSource.TrySetResult(true);
            }
        }

        class SampleHandler : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context) => context.SendLocal(new CompleteTestMessage());
        }

        class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }

    class AuditSpyEndpoint : EndpointConfigurationBuilder
    {
        public AuditSpyEndpoint() => EndpointSetup<DefaultServer, Context>((config, context) => config.Pipeline.Register(new DetectAuditMessages(context), "Detects the message being audited"));

        class DetectAuditMessages(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.ContainsKey(Headers.ControlMessageHeader))
                {
                    testContext.ControlMessageWasAudited = true;
                }

                if (!context.Message.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType))
                {
                    return Task.CompletedTask;
                }

                if (messageType.Contains(nameof(SampleMessage)))
                {
                    testContext.SampleMessageAudited = true;
                }

                if (messageType.Contains(nameof(CompleteTestMessage)))
                {
                    testContext.TestComplete = true;
                }

                return Task.CompletedTask;
            }
        }
    }

    class SampleMessage : ICommand;

    class CompleteTestMessage : ICommand;
}