using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Features;
using NServiceBus.TransactionalSession;
using NUnit.Framework;

public class When_not_using_outbox : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using (var scope = ctx.ServiceProvider.CreateScope())
                {
                    var session = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await session.Open(CancellationToken.None).ConfigureAwait(false);

                    await session.SendLocal(new SampleMessage(), CancellationToken.None).ConfigureAwait(false);

                    await session.Commit(CancellationToken.None).ConfigureAwait(false);
                }
            }))
            .Done(c => c.MessageReceived)
            .Run()
            .ConfigureAwait(false);
    }

    [Test]
    public async Task Should_not_send_messages_if_session_is_not_committed()
    {
        var result = await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
            {
                using (var scope = ctx.ServiceProvider.CreateScope())
                {
                    var session = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await session.Open(CancellationToken.None).ConfigureAwait(false);

                    await session.SendLocal(new SampleMessage(), CancellationToken.None).ConfigureAwait(false);
                }

                //Send immediately dispatched message to finish the test
                await statelessSession.SendLocal(new CompleteTestMessage(), CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.CompleteMessageReceived)
            .Run()
            .ConfigureAwait(false);

        Assert.True(result.CompleteMessageReceived);
        Assert.False(result.MessageReceived);
    }

    class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
        public bool CompleteMessageReceived { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint()
        {
            EndpointSetup<DefaultServer>((c, r) =>
            {
                c.EnableTransactionalSession();
                c.RegisterStartupTask(sp => new CaptureServiceProviderStartupTask(sp, r.ScenarioContext as Context));
            });
        }

        class SampleHandler : IHandleMessages<SampleMessage>
        {
            readonly Context context;

            public SampleHandler(Context context)
            {
                this.context = context;
            }

            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                this.context.MessageReceived = true;

                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
        {
            readonly Context context;

            public CompleteTestMessageHandler(Context context)
            {
                this.context = context;
            }

            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                this.context.CompleteMessageReceived = true;

                return Task.CompletedTask;
            }
        }

        class CaptureServiceProviderStartupTask : FeatureStartupTask
        {

            public CaptureServiceProviderStartupTask(IServiceProvider serviceProvider, Context context)
            {
                context.ServiceProvider = serviceProvider;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    class SampleMessage : ICommand
    {
    }

    class CompleteTestMessage : ICommand
    {
    }
}