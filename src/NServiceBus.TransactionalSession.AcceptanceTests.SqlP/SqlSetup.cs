using System;
using Microsoft.Data.SqlClient;
using NServiceBus;
using NServiceBus.TransactionalSession.AcceptanceTests;
using NUnit.Framework;

[SetUpFixture]
public class SqlSetup
{
    [SetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<SqlPersistence>();
            persistence.ConnectionBuilder(CreateSqlConnection);

            persistence.SqlDialect<SqlDialect.MsSqlServer>();
        };
    }

    public static SqlConnection CreateSqlConnection()
    {
        var environmentVariableName = "SQLServerConnectionString";
        var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

        if (connectionString == null)
        {
            throw new Exception($"No connection string found in environment variable {environmentVariableName}");
        }

        //HINT: this disables server certificate validation
        connectionString += ";Encrypt=False";

        return new SqlConnection(connectionString);
    }
}