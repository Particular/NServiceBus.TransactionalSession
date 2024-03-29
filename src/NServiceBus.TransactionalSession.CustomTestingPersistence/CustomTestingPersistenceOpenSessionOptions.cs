﻿namespace NServiceBus.TransactionalSession
{
    using System.Threading.Tasks;

    public class CustomTestingPersistenceOpenSessionOptions : OpenSessionOptions
    {
        public CustomTestingPersistenceOpenSessionOptions()
        {
            Extensions.Set(LoggerContextName, "TransactionalSession");
            Extensions.Set(this);
        }

        public TaskCompletionSource<bool> TransactionCommitTaskCompletionSource { get; set; }

        public bool UseTransactionScope { get; set; }

        public const string LoggerContextName = "LoggerContext";
    }
}