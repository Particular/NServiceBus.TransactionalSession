namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using Logging;
using NUnit.Framework;
using Pipeline;

public class When_using_transactional_session : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_follow_interaction_order_of_core()
    {
        var transactionalContext = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                await using var scope = ctx.ServiceProvider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions
                {
                    Metadata =
                    {
                        {CustomTestingPersistenceOpenSessionOptions.LoggerContextName, "TransactionalSession"}
                    }
                });

                await transactionalSession.SendLocal(new SomeMessage(), CancellationToken.None);

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        // due to transactional session control messages also going through the incoming pipeline we run the same scenario again
        // without the transactional session
        var pipelineContext = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (session, ctx) =>
            {
                await session.SendLocal(new SomeMessage(), CancellationToken.None);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        var transactionalSessionOrder = string.Join(Environment.NewLine, FilterLogs(transactionalContext, "TransactionalSession - "));
        var pipelineOrder = string.Join(Environment.NewLine, FilterLogs(pipelineContext, "Pipeline - "));

        Assert.That(transactionalSessionOrder, Is.EqualTo(pipelineOrder), "The transactional session order of operation is different from the core order");
    }

    static IReadOnlyCollection<string> FilterLogs(ScenarioContext scenarioContext, string filter) =>
        scenarioContext.Logs
            .Where(l => l.Level == LogLevel.Info && l.LoggerName.Contains("CustomTesting"))
            .Where(l => l.Message.StartsWith(filter))
            // Filtering out the Get since tx session doesn't do a Get in the pipeline and potentially multiple times
            .Where(l => !l.Message.Contains("Outbox.Get"))
            .OrderBy(l => l.Timestamp.Ticks)
            .Select(l => l.Message.Replace(filter, string.Empty))
            .ToArray();

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint() =>
            EndpointSetup<TransactionSessionWithOutboxEndpoint>(b => b.Pipeline.Register(new LoggerContextBehavior(), "Extracts the logger context header"));

        class LoggerContextBehavior : Behavior<ITransportReceiveContext>
        {
            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.TryGetValue(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, out var loggerContext))
                {
                    context.Extensions.Set(CustomTestingPersistenceOpenSessionOptions.LoggerContextName, loggerContext);
                }
                return next();
            }
        }

        class MessageHandler(Context testContext) : IHandleMessages<SomeMessage>
        {
            public Task Handle(SomeMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    class SomeMessage : ICommand
    {
    }
}