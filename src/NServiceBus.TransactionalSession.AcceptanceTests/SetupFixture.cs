namespace NServiceBus.TransactionalSession.AcceptanceTests;

using AcceptanceTesting;
using NUnit.Framework;

[SetUpFixture]
public class SetupFixture
{

    [OneTimeSetUp]
    public void Setup()
    {
        typeof(ITransactionalSession).ToString();
        typeof(CustomTestingPersistence).ToString();
    }
}