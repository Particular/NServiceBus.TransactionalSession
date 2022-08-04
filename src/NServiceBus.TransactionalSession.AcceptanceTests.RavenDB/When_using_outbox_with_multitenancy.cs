namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

public class When_using_outbox_with_multitenancy : NServiceBusAcceptanceTest
{
    string tenantId;
    DocumentStore documentStore;

    [SetUp]
    public void Setup()
    {
        var random = new Random();
        tenantId = "tenant-" + random.Next(1, 100);

        // replace default persistence configuration
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<RavenDBPersistence>();
            documentStore = new DocumentStore
            {
                Database = RavenSetup.DefaultDatabaseName,
                Urls = new[] { "http://localhost:8080" }
            };
            documentStore.Initialize();
            var result = documentStore.Maintenance.Server.Send(new GetDatabaseNamesOperation(0, int.MaxValue));

            if (!result.Contains(RavenSetup.DefaultDatabaseName))
            {
                documentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(RavenSetup.DefaultDatabaseName)));
            }

            if (!result.Contains(tenantId))
            {
                documentStore.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(tenantId)));
            }

            persistence.SetDefaultDocumentStore(documentStore);
            persistence.SetMessageToDatabaseMappingConvention(headers =>
            {
                if (headers.TryGetValue("tenant-id", out var tenantValue))
                {
                    return tenantValue;
                }

                return RavenSetup.DefaultDatabaseName;
            });
        };
    }

    [TearDown]
    public void Cleanup()
    {
        documentStore.Maintenance.Server.Send(new DeleteDatabasesOperation(tenantId, true));
        documentStore.Dispose();
    }

    [Test]
    public async Task Should_store_data_on_correct_tenant()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<MultitenantEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.OpenRavenDBSession(new Dictionary<string, string>() { { "tenant-id", tenantId } });
                ctx.SessionId = transactionalSession.SessionId;
                var ravenSession = transactionalSession.SynchronizedStorageSession.RavenSession();
                var document = new TestDocument() { SessionId = transactionalSession.SessionId };
                await ravenSession.StoreAsync(document);

                var sendOptions = new SendOptions();
                sendOptions.SetHeader("tenant-id", tenantId);
                sendOptions.RouteToThisEndpoint();
                await transactionalSession.Send(new SampleMessage { DocumentId = document.Id }, sendOptions, CancellationToken.None);

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.MessageReceived)
            .Run();

        Assert.NotNull(context.Document);
        Assert.AreEqual(context.SessionId, context.Document.SessionId, "should have loaded the document from the correct tenant database");
    }

    public class Context : ScenarioContext, IInjectServiceProvider
    {
        public IServiceProvider ServiceProvider { get; set; }

        public bool MessageReceived { get; set; }
        public TestDocument Document { get; set; }
        public string SessionId { get; set; }
    }

    public class MultitenantEndpoint : EndpointConfigurationBuilder
    {
        public MultitenantEndpoint()
        {
            EndpointSetup<TransactionSessionWithOutboxEndpoint>();
        }

        public class SampleMessageHandler : IHandleMessages<SampleMessage>
        {
            Context testContext;

            public SampleMessageHandler(Context testContext)
            {
                this.testContext = testContext;
            }

            public async Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                var ravenSession = context.SynchronizedStorageSession.RavenSession();
                testContext.Document = await ravenSession.LoadAsync<TestDocument>(message.DocumentId);
            }
        }
    }

    public class SampleMessage : IMessage
    {
        public string DocumentId { get; set; }
    }

    public class TestDocument
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
    }
}