namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using NUnit.Framework;
using Pipeline;

public class When_using_outbox_and_send_local : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit() =>
        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                await transactionalSession.SendLocal(new SampleMessage(), ct);

                await transactionalSession.Commit(ct).ConfigureAwait(false);
            }, afterStart: true))
            .Run();

    [Test]
    public async Task Should_not_send_messages_if_session_is_not_committed()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s =>
            {
                s.ServiceResolve(async (provider, ctx, ct) =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                    await transactionalSession.SendLocal(new SampleMessage(), ct);
                }, afterStart: true);
                s.When(async (messageSession, ctx) =>
                {
                    await messageSession.SendLocal(new CompleteTestMessage());
                });
            })
            .Done(c => c.CompleteMessageReceived)
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
        }
    }

    [Test]
    public async Task Should_not_send_control_message_when_nothing_was_sent()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s =>
            {
                s.ServiceResolve(async (provider, ctx, ct) =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);
                    // No messages sent
                    await transactionalSession.Commit(ct).ConfigureAwait(false);
                }, afterStart: true);
                s.When(async (messageSession, ctx) =>
                {
                    await messageSession.SendLocal(new CompleteTestMessage());
                });
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
            Assert.That(result.ControlMessageReceived, Is.False);
        }
    }

    [Test]
    public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);

                var sendOptions = new SendOptions();
                sendOptions.RequireImmediateDispatch();
                sendOptions.RouteToThisEndpoint();
                await transactionalSession.Send(new SampleMessage(), sendOptions, ct);
            }, afterStart: true))
            .Run();

        Assert.That(result.MessageReceived, Is.True);
        Assert.That(result.ControlMessageReceived, Is.False);
    }

    [Test]
    public async Task Should_make_it_possible_float_ambient_transactions()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions
                {
                    UseTransactionScope = true
                }, ct);

                ctx.AmbientTransactionFoundBeforeAwait = Transaction.Current != null;

                await Task.Yield();

                ctx.AmbientTransactionFoundAfterAwait = Transaction.Current != null;
            }, afterStart: true))
            .Done(c => c.EndpointsStarted)
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AmbientTransactionFoundBeforeAwait, Is.True, "The ambient transaction was not visible before the await");
            Assert.That(result.AmbientTransactionFoundAfterAwait, Is.True, "The ambient transaction was not visible after the await");
        }
    }

    class Context : TransactionalSessionTestContext
    {
        public bool AmbientTransactionFoundBeforeAwait { get; set; }
        public bool AmbientTransactionFoundAfterAwait { get; set; }
        public bool CompleteMessageReceived { get; set; }
        public bool MessageReceived { get; set; }

        public bool ControlMessageReceived { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint>(
                c => c.Pipeline.Register(typeof(DiscoverControlMessagesBehavior), "Discovers control messages"));

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }

        class DiscoverControlMessagesBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.ContainsKey(OutboxTransactionalSession.CommitDelayIncrementHeaderName))
                {
                    testContext.ControlMessageReceived = true;
                }

                await next();
            }
        }
    }

    class SampleMessage : ICommand;

    class CompleteTestMessage : ICommand;
}