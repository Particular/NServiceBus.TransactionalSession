namespace NServiceBus.AcceptanceTesting;

using System.Threading.Tasks;
using TransactionalSession;

public class CustomPersistenceOpenSessionOptions : OpenSessionOptions
{
    public CustomPersistenceOpenSessionOptions()
    {
        Extensions.Set(this);
    }

    public TaskCompletionSource<bool> TransactionCommitTaskCompletionSource { get; set; }
}