namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using NUnit.Framework;

    [ExecuteOnlyForEnvironmentWith(EnvironmentVariables.CosmosConnectionString)]
    public class When_using_outbox : NServiceBusAcceptanceTest
    {
        static string PartitionKeyHeaderName = "Tests.PartitionKey";
        static string PartitionKeyValue = "SomePartition";

        [Test]
        public async Task Should_send_messages_and_store_document_on_transactional_session_commit()
        {
            var documentId = Guid.NewGuid().ToString();

            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.OpenCosmosDBSession(PartitionKeyValue);

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    sendOptions.RouteToThisEndpoint();

                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);

                    var storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                    storageSession.Batch.CreateItem(new MyDocument
                    {
                        Id = documentId,
                        Data = "SomeData",
                        PartitionKey = PartitionKeyValue
                    });

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            var response = await CosmosSetup.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(PartitionKeyValue));

            Assert.IsNotNull(response);
            Assert.AreEqual("SomeData", response.Resource.Data);
        }

        [Test]
        public async Task Should_not_send_messages_if_session_is_not_committed()
        {
            var documentId = Guid.NewGuid().ToString();

            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (statelessSession, ctx) =>
                {
                    using (var scope = ctx.ServiceProvider.CreateScope())
                    using (var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>())
                    {
                        await transactionalSession.OpenCosmosDBSession(PartitionKeyValue);

                        await transactionalSession.SendLocal(new SampleMessage());

                        var storageSession = transactionalSession.SynchronizedStorageSession.CosmosPersistenceSession();
                        storageSession.Batch.CreateItem(new MyDocument
                        {
                            Id = documentId,
                            Data = "SomeData",
                            PartitionKey = PartitionKeyValue
                        });
                    }

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    sendOptions.RouteToThisEndpoint();

                    //Send immediately dispatched message to finish the test
                    await statelessSession.Send(new CompleteTestMessage(), sendOptions);
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();


            Assert.True(result.CompleteMessageReceived);
            Assert.False(result.MessageReceived);

            var exception = Assert.ThrowsAsync<CosmosException>(async () =>
                await CosmosSetup.Container.ReadItemAsync<MyDocument>(documentId, new PartitionKey(PartitionKeyValue)));
            Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
        }

        [Test]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.OpenCosmosDBSession(PartitionKeyValue);

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    sendOptions.RequireImmediateDispatch();
                    sendOptions.RouteToThisEndpoint();
                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);
                }))
                .Done(c => c.MessageReceived)
                .Run()
                ;

            Assert.True(result.MessageReceived);
        }

        class Context : ScenarioContext, IInjectServiceProvider
        {
            public bool MessageReceived { get; set; }
            public bool CompleteMessageReceived { get; set; }
            public IServiceProvider ServiceProvider { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() =>
                EndpointSetup<TransactionSessionWithOutboxEndpoint>(c =>
                {
                    var persistence = c.UsePersistence<CosmosPersistence>();

                    persistence.TransactionInformation().ExtractPartitionKeyFromHeader(PartitionKeyHeaderName);
                });

            class SampleHandler : IHandleMessages<SampleMessage>
            {
                public SampleHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(SampleMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }

            class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
            {
                public CompleteTestMessageHandler(Context context) => testContext = context;

                public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
                {
                    testContext.CompleteMessageReceived = true;

                    return Task.CompletedTask;
                }

                readonly Context testContext;
            }
        }

        class SampleMessage : ICommand
        {
        }

        class CompleteTestMessage : ICommand
        {
        }

        class MyDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            public string Data { get; set; }

            [JsonProperty(CosmosSetup.PartitionPropertyName)]
            public string PartitionKey { get; set; }
        }
    }
}