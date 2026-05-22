namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AcceptanceTesting;
using Logging;
using NUnit.Framework;
using Pipeline;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

public class When_using_endpoint_logging_scope : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_enrich_logs_with_endpoint_scope_during_session_operations()
    {
        var result = await Scenario.Define<Context>(ctx => ctx.IncludeLoggingScopes = true)
            .WithEndpoint<EndpointWithScope>(s => s.When(async (_, ctx) =>
            {
                var logger = ctx.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SessionScopeLogger");

                await using var scope = ctx.ServiceProvider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                logger.LogInformation("Log inside transactional session scope");

                await transactionalSession.SendLocal(new CompleteTestMessage());
                await transactionalSession.Commit();
            }))
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.MessageReceived, Is.True);
            Assert.That(result.Logs, Has.One.Matches<ScenarioContext.LogItem>(l =>
                l.LoggerName == "SessionScopeLogger" &&
                (l.Message ?? string.Empty).Contains("Log inside transactional session scope") &&
                (l.Message ?? string.Empty).Contains("Endpoint = ") &&
                (l.Message ?? string.Empty).Contains("EndpointIdentifier = ")),
                "Log written during transactional session operations should include the endpoint logging scope");
        }
    }

    [Test]
    public async Task Should_not_duplicate_scope_when_pipeline_also_calls_begin_endpoint_scope()
    {
        var result = await Scenario.Define<Context>(ctx => ctx.IncludeLoggingScopes = true)
            .WithEndpoint<EndpointWithBehavior>(s => s.When(async (_, ctx) =>
            {
                await using var scope = ctx.ServiceProvider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());
                await transactionalSession.SendLocal(new CompleteTestMessage());
                await transactionalSession.Commit();
            }))
            .Run();

        Assert.That(result.MessageReceived, Is.True);

        var behaviorLog = result.Logs.FirstOrDefault(l =>
            l.LoggerName.EndsWith("OutgoingLoggingBehavior") &&
            (l.Message ?? string.Empty).Contains("Behavior executed"));

        Assert.That(behaviorLog?.Message, Is.Not.Null);
        Assert.That(CountOccurrences(behaviorLog!.Message!, "Endpoint = "), Is.EqualTo(1),
            "Behavior: endpoint scope should appear exactly once, not duplicated by BeginEndpointScope");
    }

    static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
    }

    class EndpointWithScope : EndpointConfigurationBuilder
    {
        public EndpointWithScope() => EndpointSetup<TransactionSessionDefaultServer>();

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    class EndpointWithBehavior : EndpointConfigurationBuilder
    {
        public EndpointWithBehavior() => EndpointSetup<TransactionSessionDefaultServer>(c =>
            c.Pipeline.Register<OutgoingLoggingBehavior>("Logs in outgoing pipeline to verify no scope duplication"));

        class CompleteTestMessageHandler(Context testContext) : IHandleMessages<CompleteTestMessage>
        {
            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }

        class OutgoingLoggingBehavior(EndpointLoggingScope endpointScope, ILogger<OutgoingLoggingBehavior> logger) : Behavior<IOutgoingLogicalMessageContext>
        {
            public override Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
            {
                using (logger.BeginEndpointScope(endpointScope))
                {
                    logger.LogInformation("Behavior executed");
                }

                return next();
            }
        }
    }

    class CompleteTestMessage : ICommand;
}
