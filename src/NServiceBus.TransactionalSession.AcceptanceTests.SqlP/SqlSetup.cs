namespace NServiceBus.TransactionalSession.AcceptanceTests
{

    using System;
    using Microsoft.Data.SqlClient;
    using NServiceBus;

    public class SqlSetup
    {
        public static void Setup()
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
            var connectionString = EnvironmentHelper.GetEnvironmentVariable(EnvironmentVariables.SqlServerConnectionString);

            if (connectionString == null)
            {
                throw new Exception($"No connection string found in environment variable {EnvironmentVariables.SqlServerConnectionString}");
            }

            //HINT: this disables server certificate validation
            connectionString += ";Encrypt=False";

            return new SqlConnection(connectionString);
        }
    }
}