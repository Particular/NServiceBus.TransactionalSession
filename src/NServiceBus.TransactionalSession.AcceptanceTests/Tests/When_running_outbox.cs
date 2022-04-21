using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Features;
using NServiceBus.TransactionalSession;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NServiceBus.TransactionalSession.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_running_outbox : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>()
            .Done(c => c.MessageReceived)
            .Run()
            .ConfigureAwait(false);
    }

    class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint()
        {
            EndpointSetup<DefaultServer>((c, r) =>
            {
                c.EnableOutbox();

                c.EnableFeature<TransactionalSessionFeature>();

                c.RegisterStartupTask(sp => new SendMessageViaTransactionalSession(sp));
            });
        }

        class SendMessageViaTransactionalSession : FeatureStartupTask
        {
            readonly IServiceProvider serviceProvider;

            public SendMessageViaTransactionalSession(IServiceProvider serviceProvider)
            {
                this.serviceProvider = serviceProvider;
            }

            protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                using (var scope = serviceProvider.CreateScope())
                {
                    var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.Open(cancellationToken).ConfigureAwait(false);

                    await transactionalSession.SendLocal(new SampleMessage(), cancellationToken).ConfigureAwait(false);

                    await transactionalSession.Commit(cancellationToken).ConfigureAwait(false);
                }
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    public class SampleMessage : IMessage
    {
    }
}