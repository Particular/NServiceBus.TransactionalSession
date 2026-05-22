namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using NUnit.Framework;

public class When_multiple_endpoints_use_transactional_session : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_independently_commit_transactions()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<EndpointAlpha>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);
                await transactionalSession.SendLocal(new AlphaMessage(), ct);
                await transactionalSession.Commit(ct);
            }, afterStart: true))
            .WithEndpoint<EndpointBeta>(s => s.ServiceResolve(async (provider, ctx, ct) =>
            {
                await using var scope = provider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);
                await transactionalSession.SendLocal(new BetaMessage(), ct);
                await transactionalSession.Commit(ct);
            }, afterStart: true))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AlphaMessageReceived, Is.True, "EndpointAlpha should receive its own message");
            Assert.That(result.BetaMessageReceived, Is.True, "EndpointBeta should receive its own message");
        }
    }

    [Test]
    public async Task Should_allow_one_to_commit_and_other_to_rollback_independently()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<EndpointAlpha>(s =>
            {
                s.ServiceResolve(async (provider, ctx, ct) =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);
                    await transactionalSession.SendLocal(new AlphaMessage(), ct);
                    // Commit Alpha
                    await transactionalSession.Commit(ct);
                }, afterStart: true);
                s.When(async (messageSession, ctx) =>
                {
                    await messageSession.SendLocal(new CompleteTestMessage());
                });
            })
            .WithEndpoint<EndpointBeta>(s =>
            {
                s.ServiceResolve(async (provider, ctx, ct) =>
                {
                    await using var scope = provider.CreateAsyncScope();
                    await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions(), ct);
                    await transactionalSession.SendLocal(new BetaMessage(), ct);
                    // Do NOT commit Beta - just dispose
                }, afterStart: true);
            })
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AlphaMessageReceived, Is.True, "Alpha committed, so its message should be dispatched");
            Assert.That(result.BetaMessageReceived, Is.False, "Beta did not commit, so its message should not be dispatched");
            Assert.That(result.CompleteMessageReceived, Is.True);
        }
    }

    class Context : TransactionalSessionTestContext
    {
        public bool AlphaMessageReceived { get; set; }
        public bool BetaMessageReceived { get; set; }
        public bool CompleteMessageReceived { get; set; }

        public void MaybeCompleted() => MarkAsCompleted(
            AlphaMessageReceived,
            BetaMessageReceived || CompleteMessageReceived);
    }

    class EndpointAlpha : EndpointConfigurationBuilder
    {
        public EndpointAlpha() => EndpointSetup<TransactionSessionDefaultServer>();

        class AlphaMessageHandler(Context testContext) : IHandleMessages<AlphaMessage>
        {
            public Task Handle(AlphaMessage message, IMessageHandlerContext context)
            {
                testContext.AlphaMessageReceived = true;
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class EndpointBeta : EndpointConfigurationBuilder
    {
        public EndpointBeta() => EndpointSetup<TransactionSessionDefaultServer>();

        class BetaMessageHandler(Context testContext) : IHandleMessages<BetaMessage>
        {
            public Task Handle(BetaMessage message, IMessageHandlerContext context)
            {
                testContext.BetaMessageReceived = true;
                testContext.MaybeCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class AlphaMessage : ICommand;

    class BetaMessage : ICommand;

    class CompleteTestMessage : ICommand;
}
