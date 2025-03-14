﻿namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using NUnit.Framework;

public class When_using_outbox_send_only : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<SendOnlyEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                await transactionalSession.Open(new CustomTestingPersistenceOpenSessionOptions());

                var options = new SendOptions();

                options.SetDestination(Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)));

                await transactionalSession.Send(new SampleMessage(), options);

                await transactionalSession.Commit(CancellationToken.None);
            }))
            .WithEndpoint<AnotherEndpoint>()
            .WithEndpoint<ProcessorEndpoint>()
            .Done(c => c.MessageReceived)
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    class Context : ScenarioContext, IInjectServiceProvider
    {
        public bool MessageReceived { get; set; }

        public IServiceProvider ServiceProvider { get; set; }
    }

    class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() => EndpointSetup<DefaultServerWithServiceProviderCapturing>(c =>
        {
            var options = new TransactionalSessionOptions { ProcessorAddress = Conventions.EndpointNamingConvention.Invoke(typeof(AnotherEndpoint)) };
            var persistence = c.UsePersistence<CustomTestingPersistence>();
            persistence.EnableTransactionalSession(options);

            c.EnableOutbox();
            c.SendOnly();
        });
    }

    class AnotherEndpoint : EndpointConfigurationBuilder
    {
        public AnotherEndpoint() => EndpointSetup<TransactionSessionDefaultServer>();

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
        public ProcessorEndpoint() => EndpointSetup<TransactionSessionWithOutboxEndpoint>();
    }

    class SampleMessage : ICommand
    {
    }
}