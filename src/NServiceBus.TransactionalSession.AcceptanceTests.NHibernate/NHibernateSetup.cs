namespace NServiceBus.TransactionalSession.AcceptanceTests.NHibernate;

using System;
using NUnit.Framework;
using Persistence;

[SetUpFixture]
public class NHibernateSetup
{
    [OneTimeSetUp]
    public void Setup()
    {
        TransactionSessionDefaultServer.ConfigurePersistence = configuration =>
        {
            var persistence = configuration.UsePersistence<NHibernatePersistence>();

            var environmentVariableName = "SQLServerConnectionString";
            var connectionString = Environment.GetEnvironmentVariable(environmentVariableName);

            if (connectionString == null)
            {
                throw new Exception($"No connection string found in environment variable {environmentVariableName}");
            }

            persistence.ConnectionString(connectionString);
        };
    }
}