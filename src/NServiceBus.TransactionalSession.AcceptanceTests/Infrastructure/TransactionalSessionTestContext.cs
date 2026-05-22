namespace NServiceBus.TransactionalSession.AcceptanceTests;

using System.Collections.Concurrent;
using System.Threading.Tasks;
using AcceptanceTesting;

public class TransactionalSessionTestContext : ScenarioContext
{
    public CustomTestingDatabase Database { get; } = new();

    public TaskCompletionSource<bool> TransactionTaskCompletionSource { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
