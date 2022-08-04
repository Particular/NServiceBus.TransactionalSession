namespace NServiceBus.TransactionalSession.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using AcceptanceTesting;
    using Microsoft.Azure.Cosmos.Table;
    using NUnit.Framework;
    using Persistence.AzureTable;
    using Pipeline;
    using System.Linq;

    public class When_using_outbox : NServiceBusAcceptanceTest
    {
        static string PartitionKeyHeaderName = "Tests.PartitionKey";
        static string PartitionKeyValue = "SomePartition";

        [Test]
        public async Task Should_send_messages_and_store_entity_on_transactional_session_commit()
        {
            var entityRowId = Guid.NewGuid().ToString();

            await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();
                    await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue, AzureTableFixture.TableName);

                    var sendOptions = new SendOptions();
                    sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    sendOptions.RouteToThisEndpoint();

                    await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);

                    var storageSession = transactionalSession.SynchronizedStorageSession.AzureTablePersistenceSession();

                    var entity = new MyTableEntity
                    {
                        RowKey = entityRowId,
                        PartitionKey = PartitionKeyValue,
                        Data = "MyCustomData"
                    };

                    storageSession.Batch.Add(TableOperation.Insert(entity));

                    await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
                }))
                .Done(c => c.MessageReceived)
                .Run();

            var query = new TableQuery<DynamicTableEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, PartitionKeyValue))
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, entityRowId));

            var tableEntity = AzureTableFixture.Table.ExecuteQuery(query).FirstOrDefault();

            Assert.IsNotNull(tableEntity);
            Assert.AreEqual(tableEntity.Properties["Data"].StringValue, "MyCustomData");
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
                        await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue, AzureTableFixture.TableName);

                        var sendOptions = new SendOptions();
                        sendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                        sendOptions.RouteToThisEndpoint();

                        await transactionalSession.Send(new SampleMessage(), sendOptions);
                    }

                    var competeMessageSendOptions = new SendOptions();
                    competeMessageSendOptions.SetHeader(PartitionKeyHeaderName, PartitionKeyValue);
                    competeMessageSendOptions.RouteToThisEndpoint();

                    //Send immediately dispatched message to finish the test
                    await statelessSession.Send(new CompleteTestMessage(), competeMessageSendOptions);
                }))
                .Done(c => c.CompleteMessageReceived)
                .Run();

            Assert.True(result.CompleteMessageReceived);
            Assert.False(result.MessageReceived);
        }

        [Test]
        public async Task Should_send_immediate_dispatch_messages_even_if_session_is_not_committed()
        {
            var result = await Scenario.Define<Context>()
                .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
                {
                    using var scope = ctx.ServiceProvider.CreateScope();
                    using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                    await transactionalSession.OpenAzureTableSession(PartitionKeyHeaderName, PartitionKeyValue);

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
                    c.Pipeline.Register(sp => new PartitionKeyProviderBehavior(), "Extract partition key value from message header.");
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

        public class MyTableEntity : TableEntity
        {
            public string Data { get; set; }
        }

        class PartitionKeyProviderBehavior : Behavior<ITransportReceiveContext>
        {
            public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.TryGetValue(PartitionKeyHeaderName, out var partitionKeyValue))
                {
                    context.Extensions.Set(new TableEntityPartitionKey(partitionKeyValue));
                }

                return next();
            }
        }
    }
}