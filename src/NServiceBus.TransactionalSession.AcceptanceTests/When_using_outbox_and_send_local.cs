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
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                await transactionalSession.SendLocal(new SampleMessage(), CancellationToken.None);

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
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                    await transactionalSession.SendLocal(new SampleMessage());
                }

                //Send immediately dispatched message to finish the test
                await statelessSession.SendLocal(new CompleteTestMessage());
            }))
            .Done(c => c.CompleteMessageReceived)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
        });
    }

    [Test]
    public async Task Should_not_send_control_message_when_nothing_was_sent()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
            {
                using (var scope = ctx.ServiceProvider.CreateScope())
                using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                {
                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());
                    // No messages sent
                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }

                //Send immediately dispatched message to finish the test
                await statelessSession.SendLocal(new CompleteTestMessage());
            }))
            .Done(c => c.CompleteMessageReceived)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.CompleteMessageReceived, Is.True);
            Assert.That(result.MessageReceived, Is.False);
            Assert.That(result.ControlMessageReceived, Is.False);
        });
    }

    [Test]
    public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                var sendOptions = new SendOptions();
                sendOptions.RequireImmediateDispatch();
                sendOptions.RouteToThisEndpoint();
                await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(result.MessageReceived, Is.True);
        Assert.That(result.ControlMessageReceived, Is.False);
    }

    [Test]
    public async Task Should_make_it_possible_float_ambient_transactions()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions
                {
                    UseTransactionScope = true
                });

                ctx.AmbientTransactionFoundBeforeAwait = Transaction.Current != null;

                await Task.Yield();

                ctx.AmbientTransactionFoundAfterAwait = Transaction.Current != null;
            }))
            .Done(c => c.EndpointsStarted)
            .Run();

        Assert.Multiple(() =>
        {
            Assert.That(result.AmbientTransactionFoundBeforeAwait, Is.True, "The ambient transaction was not visible before the await");
            Assert.That(result.AmbientTransactionFoundAfterAwait, Is.True, "The ambient transaction was not visible after the await");
        });
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

                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;

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

    class SampleMessage : ICommand
    {
    }

    class CompleteTestMessage : ICommand
    {
    }
}