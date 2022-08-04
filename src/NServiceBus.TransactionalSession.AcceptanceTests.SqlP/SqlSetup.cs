using System;
using Microsoft.Data.SqlClient;
using NServiceBus;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NUnit.Framework;

[SetUpFixture]
public class SqlSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<SqlPersistence>();
            persistence.ConnectionBuilder(() =>
            {
                var environmentVariableName = "SQLServerConnectionString";
                var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

                if (connectionString == null)
                {
                    throw new Exception($"No connection string found in environment variable {environmentVariableName}");
                }

                return new SqlConnection(connectionString);
            });

            persistence.SqlDialect<SqlDialect.MsSqlServer>();
        };
    }
}