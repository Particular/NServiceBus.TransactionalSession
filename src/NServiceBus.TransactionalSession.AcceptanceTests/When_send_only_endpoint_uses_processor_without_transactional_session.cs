namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using NUnit.Framework;
using Pipeline;

public class When_send_only_endpoint_uses_processor_without_transactional_session : NServiceBusAcceptanceTest
{
    // This test verifies a specific configuration scenario: a send-only endpoint with transactional session enabled
    // that uses a separate processor endpoint which doesn't have transactional session enabled.
    //
    // Under normal load conditions, forgetting to enable the transactional session in the processor endpoint
    // might still allow transactions to complete successfully. However, under pressure or high load conditions,
    // the missing TransactionalSessionDelayControlMessageBehavior from the Transactional Session
    // that should have been enabled in the processor endpoint becomes critical.
    // 
    // When properly configured, this behavior delays processing of control messages to ensure the transactional
    // session commit has enough time to complete. Without this behavior, the outbox might process the control
    // message before the commit completes, creating a tombstone record that will result 
    // in the transactional session running in the send-only endpoint to roll back the transaction.
    //
    // This test specifically verifies that:
    // 1. When this configuration issue exists (send-only with separate processor missing transactional session)
    // 2. Under pressure conditions (simulated with delays), a tombstone record is created
    // 3. A specific, detailed warning for send-only endpoints is logged that guides developers to the root cause:
    //    "...if you have forgotten to enable transactional session in the processor endpoint"    
    //
    // This helps developers identify and fix the configuration issue that
    // may only manifest under production load conditions.
    [Test]
    public async Task Should_log_specific_warning_about_missing_processor_configuration()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SendOnlyEndpoint>(s => s.When(async (_, ctx) =>
            {
                await using var scope = ctx.ServiceProvider.CreateAsyncScope();
                await using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                try
                {
                    ctx.TransactionTaskCompletionSource =
                        new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var options = new CustomTestingPersistenceOpenSessionOptions
                    {
                        CommitDelayIncrement = TimeSpan.FromSeconds(1),
                        MaximumCommitDuration = TimeSpan.FromSeconds(8),
                        TransactionCommitTaskCompletionSource = ctx.TransactionTaskCompletionSource
                    };

                    await transactionalSession.Open(options);
                    var sendOptions = new SendOptions();

                    sendOptions.SetDestination(Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)));

                    await transactionalSession.Send(new SampleMessage(), sendOptions);

                    await transactionalSession.Commit(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    ctx.TransactionalSessionException = exception;
                }
            }))
            .WithEndpoint<AnotherEndpoint>()
            .WithEndpoint<ProcessorEndpoint>()
            .Done(c => c.TransactionalSessionException != null)
            .Run();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(context.MessageReceived, Is.False);
            Assert.That(
                context.TransactionalSessionException.Message,
                Does.StartWith(
                    "Failed to commit the transactional session. This might happen if the maximum commit duration is exceeded or if the transactional session has not been enabled on the configured processor endpoint - "));
        }
    }

    class Context : TransactionalSessionTestContext
    {
        public bool MessageReceived { get; set; }
        public TaskCompletionSource<bool> TransactionTaskCompletionSource { get; set; }
        public Exception TransactionalSessionException { get; set; }
    }

    class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() => EndpointSetup<DefaultServer>(c =>
        {
            var options = new TransactionalSessionOptions
            {
                ProcessorEndpoint = Conventions.EndpointNamingConvention.Invoke(typeof(ProcessorEndpoint))
            };

            c.GetSettings().Get<PersistenceExtensions<CustomTestingPersistence>>().EnableTransactionalSession(options);

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
    {
        public AnotherEndpoint() => EndpointSetup<DefaultServer>();

        class SampleHandler(Context testContext) : IHandleMessages<SampleMessage>
        {
            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }
    }

    class ProcessorEndpoint : EndpointConfigurationBuilder
    {
        public ProcessorEndpoint() => EndpointSetup<DefaultServer>((c, r) =>
        {
            c.Pipeline.Register(new UnblockCommitBehavior((Context)r.ScenarioContext),
                "unblocks the transactional session commit operation");

            c.ConfigureTransport().TransportTransactionMode = TransportTransactionMode.ReceiveOnly;
            // Only enables the outbox and deliberately NOT enabling the transactional session
            c.EnableOutbox();
        });

        class UnblockCommitBehavior(Context testContext) : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                // This callback is set in the pipeline context so the outbox storage can retrieve it and unlock the transactional session commit operation
                // allowing us to control in which order the outbox storage and the transactional session commit operations are executed.
                context.Extensions.Set("TestOutboxStorage.StoreCallback", () =>
                {
                    testContext.TransactionTaskCompletionSource.TrySetResult(true);
                });

                await next();
            }
        }
    }

    class SampleMessage : ICommand;
}
