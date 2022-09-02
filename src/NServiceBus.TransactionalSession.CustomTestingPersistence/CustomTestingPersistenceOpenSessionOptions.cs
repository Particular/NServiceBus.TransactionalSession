namespace NServiceBus.AcceptanceTesting;

using System.Threading.Tasks;
using TransactionalSession;

public class CustomTestingPersistenceOpenSessionOptions : OpenSessionOptions
{
    public CustomTestingPersistenceOpenSessionOptions() => Extensions.Set(this);

    public TaskCompletionSource<bool> TransactionCommitTaskCompletionSource { get; set; }
}