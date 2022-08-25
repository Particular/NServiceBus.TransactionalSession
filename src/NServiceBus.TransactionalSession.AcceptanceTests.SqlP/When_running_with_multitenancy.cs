﻿namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.Sql;

[ExecuteOnlyForEnvironmentWith(EnvironmentVariables.SqlServerConnectionString)]
public class When_running_with_multitenancy : NServiceBusAcceptanceTest
{
    static readonly string tenantId = "aTenant";
    static readonly string tenantIdHeaderName = "TenantName";

    [OneTimeSetUp]
    public async Task Setup()
    {
        await MultiTenant.Setup(tenantId);
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            configuration.EnableOutbox().DisableCleanup();

            PersistenceExtensions<SqlPersistence> persistence = configuration.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>();

            persistence.MultiTenantConnectionBuilder(tenantIdHeaderName, tenantId => MultiTenant.Build(tenantId));
        };
    }

    [OneTimeTearDown]
    public Task Teardown() => MultiTenant.TearDown(tenantId);

    [Test]
    public async Task Should_send_messages_on_transactional_session_commit()
    {
        await MultiTenant.CreateOutboxTable(tenantId, Conventions.EndpointNamingConvention(typeof(AnEndpoint)));

        await Scenario.Define<Context>()
            .WithEndpoint<AnEndpoint>(s => s.When(async (_, ctx) =>
            {
                using IServiceScope scope = ctx.ServiceProvider.CreateScope();
                using ITransactionalSession transactionalSession =
                    scope.ServiceProvider.GetRequiredService<ITransactionalSession>();

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
            EndpointSetup<TransactionSessionWithOutboxEndpoint>();

        class SampleHandler : IHandleMessages<SampleMessage>
        {
            readonly Context testContext;

            public SampleHandler(Context testContext) => this.testContext = testContext;

            public Task Handle(SampleMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;

                return Task.CompletedTask;
            }
        }

        class CompleteTestMessageHandler : IHandleMessages<CompleteTestMessage>
        {
            readonly Context testContext;

            public CompleteTestMessageHandler(Context context) => testContext = context;

            public Task Handle(CompleteTestMessage message, IMessageHandlerContext context)
            {
                testContext.CompleteMessageReceived = true;

                return Task.CompletedTask;
            }
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
            string dbName = "nservicebus_" + tenantId.ToLower();

            using (var connection = new SqlConnection(GetBaseConnectionString()))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                using (SqlCommand dropCommand = connection.CreateCommand())
                {
                    dropCommand.CommandText =
                        $"if not exists (select * from sysdatabases where name = '{dbName}') create database {dbName};";
                    await dropCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task CreateOutboxTable(string tenantId, string endpointName)
        {
            string tablePrefix = $"{endpointName.Replace('.', '_')}_";
            var dialect = new SqlDialect.MsSqlServer();

            string scriptDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NServiceBus.Persistence.Sql",
                dialect.GetType().Name);

            await ScriptRunner.Install(dialect, tablePrefix, () => Build(tenantId), scriptDirectory, true, false, false,
                CancellationToken.None).ConfigureAwait(false);
        }

        public static Task TearDown(string tenantId)
        {
            string dbName = "nservicebus_" + tenantId.ToLower();
            return DropDatabase(dbName);
        }

        public static SqlConnection Build(string tenantId)
        {
            string connection = GetBaseConnectionString()
                .Replace(";Database=nservicebus;", $";Database=nservicebus_{tenantId.ToLower()};")
                .Replace(";Initial Catalog=nservicebus;", $";Initial Catalog=nservicebus_{tenantId.ToLower()};");
            return new SqlConnection(connection);
        }

        static string GetBaseConnectionString()
        {
            string connection = EnvironmentHelper.GetEnvironmentVariable(EnvironmentVariables.SqlServerConnectionString);
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new Exception("SQLServerConnectionString environment variable is empty");
            }

            if (!connection.Contains(";Database=nservicebus;") && !connection.Contains(";Initial Catalog=nservicebus"))
            {
                throw new Exception(
                    "Environment variable `SQLServerConnectionString` must include a connection string that specifies a database name of `nservicebus` to test multi-tenant operations.");
            }

            //HINT: this disables server certificate validation
            connection += ";Encrypt=False";
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

                using (SqlCommand dropCommand = connection.CreateCommand())
                {
                    dropCommand.CommandText =
                        $"use master; if exists(select * from sysdatabases where name = '{databaseName}') begin alter database {databaseName} set SINGLE_USER with rollback immediate; drop database {databaseName}; end; ";
                    await dropCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }
    }
}