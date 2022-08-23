namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System;
using NUnit.Framework;
using Persistence;

[SetUpFixture]
[EnvironmentSpecificTest(EnvironmentVariables.SqlServerConnectionString)]
public class NHibernateSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<NHibernatePersistence>();

            string connectionString = GetConnectionString();

            persistence.ConnectionString(connectionString);
        };
    }

    public static string GetConnectionString()
    {
        var environmentVariableName = EnvironmentVariables.SqlServerConnectionString;
        var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

        if (connectionString == null)
        {
            throw new Exception($"No connection string found in environment variable {environmentVariableName}");
        }

        return connectionString;
    }
}