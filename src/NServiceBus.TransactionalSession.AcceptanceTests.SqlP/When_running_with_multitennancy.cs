namespace NServiceBus.TransactionalSession.AcceptanceTests.SqlP;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.Sql;

public class When_running_with_multitennancy : NServiceBusAcceptanceTest
{
    static string tenantId = "aTenant";
    static string tenantIdHeaderName = "TenantName";

    [SetUp]
    public Task Setup() => MultiTenant.Setup(tenantId);

    [TearDown]
    public Task Teardown() => MultiTenant.TearDown(tenantId);

    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await MultiTenant.CreateOutboxTable(tenantId, AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint)));

        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using var scope = ctx.ServiceProvider.CreateScope();
                using var transactionalSession = scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

                await transactionalSession.OpenSqlSession(tenantIdHeaderName, tenantId);

                var sendOptions = new SendOptions();
                sendOptions.SetHeader(tenantIdHeaderName, tenantId);
                sendOptions.RouteToThisEndpoint();

                await transactionalSession.Send(new SampleMessage(), sendOptions, CancellationToken.None);

                await transactionalSession.Commit(CancellationToken.None).ConfigureAwait(false);
            }))
            .Done(c => c.MessageReceived)
            .Run();
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
                c.EnableOutbox().DisableCleanup();

                var persistence = c.UsePersistence<SqlPersistence>();
                persistence.SqlDialect<SqlDialect.MsSqlServer>();

                persistence.MultiTenantConnectionBuilder(tenantIdHeaderName, tenantId => MultiTenant.Build(tenantId));

                c.EnableInstallers();
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

    public static class MultiTenant
    {
        public static async Task Setup(string tenantId)
        {
            var dbName = "nservicebus_" + tenantId.ToLower();

            using (var connection = new SqlConnection(GetBaseConnectionString()))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                using (var dropCommand = connection.CreateCommand())
                {
                    dropCommand.CommandText = $"if not exists (select * from sysdatabases where name = '{dbName}') create database {dbName};";
                    await dropCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task CreateOutboxTable(string tenantId, string endpointName)
        {
            var tablePrefix = $"{endpointName.Replace('.', '_')}_";
            var dialect = new SqlDialect.MsSqlServer();

            var scriptDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NServiceBus.Persistence.Sql",
                dialect.GetType().Name);

            await ScriptRunner.Install(dialect, tablePrefix, () => Build(tenantId), scriptDirectory, true, false, false,
                CancellationToken.None).ConfigureAwait(false);
        }

        public static Task TearDown(string tenantId)
        {
            var dbName = "nservicebus_" + tenantId.ToLower();
            return DropDatabase(dbName);
        }

        public static SqlConnection Build(string tenantId)
        {
            var connection = GetBaseConnectionString()
                .Replace(";Database=nservicebus;", $";Database=nservicebus_{tenantId.ToLower()};")
                .Replace(";Initial Catalog=nservicebus;", $";Initial Catalog=nservicebus_{tenantId.ToLower()};");
            return new SqlConnection(connection);
        }

        static string GetBaseConnectionString()
        {
            var connection = Environment.GetEnvironmentVariable("SQLServerConnectionString");
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new Exception("SQLServerConnectionString environment variable is empty");
            }

            if (!connection.Contains(";Database=nservicebus;") && !connection.Contains(";Initial Catalog=nservicebus"))
            {
                throw new Exception("Environment variable `SQLServerConnectionString` must include a connection string that specifies a database name of `nservicebus` to test multi-tenant operations.");
            }

            return connection;
        }

        static async Task DropDatabase(string databaseName)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(GetBaseConnectionString())
            {
                InitialCatalog = "master"
            };

            using (var connection = new SqlConnection(connectionStringBuilder.ToString()))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                using (var dropCommand = connection.CreateCommand())
                {
                    dropCommand.CommandText = $"use master; if exists(select * from sysdatabases where name = '{databaseName}') begin alter database {databaseName} set SINGLE_USER with rollback immediate; drop database {databaseName}; end; ";
                    await dropCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }
    }
}