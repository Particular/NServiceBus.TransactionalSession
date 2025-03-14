namespace NServiceBus.TransactionalSession;

using System.Threading.Tasks;

public class CustomTestingPersistenceOpenSessionOptions : OpenSessionOptions
{
    public CustomTestingPersistenceOpenSessionOptions()
    {
        Extensions.Set(LoggerContextName, "TransactionalSession");
        Extensions.Set(this);
    }

    public TaskCompletionSource<bool> TransactionCommitTaskCompletionSource { get; init; }

    public bool UseTransactionScope { get; init; }

    public const string LoggerContextName = "LoggerContext";
}