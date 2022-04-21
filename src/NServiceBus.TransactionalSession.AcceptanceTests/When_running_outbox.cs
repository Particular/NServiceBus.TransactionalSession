using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Features;
using NServiceBus.TransactionalSession;
using NServiceBus.TransactionalSession.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_running_outbox
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When((ms, ctx) =>
            {
                ctx.Started = true;
                return Task.CompletedTask;
            }))
            .Done(c => c.MessageReceived)
            .Run()
            .ConfigureAwait(false);
    }

    class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
        public bool Started { get; set; }
    }

    class AnEndpoint : EndpointConfigurationBuilder
    {
        public AnEndpoint()
        {
            EndpointSetup<DefaultEndpoint>((c, r) =>
            {
                c.EnableOutbox();

                c.EnableFeature<TransactionalSessionFeature>();

                c.RegisterStartupTask(sp => new SendMessageViaTransactionalSession(sp, r.ScenarioContext as Context));
            });
        }

        class SendMessageViaTransactionalSession : FeatureStartupTask
        {
            readonly IServiceProvider serviceProvider;
            readonly Context context;

            public SendMessageViaTransactionalSession(IServiceProvider serviceProvider, Context context)
            {
                this.serviceProvider = serviceProvider;
                this.context = context;
            }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
            {
                _ = Task.Run(async () =>
                {
                    while (context.Started == false)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
                    }

                    using (var scope = serviceProvider.CreateScope())
                    {
                        var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                        await transactionalSession.Open(cancellationToken).ConfigureAwait(false);

                        await transactionalSession.SendLocal(new SampleMessage(), cancellationToken)
                            .ConfigureAwait(false);

                        await transactionalSession.Commit(cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken).ConfigureAwait(false);

                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    class SampleMessage : ICommand
    {
    }
}