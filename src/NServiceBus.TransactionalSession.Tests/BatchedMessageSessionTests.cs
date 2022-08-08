namespace NServiceBus.TransactionalSession.Tests;

using System.Linq;
using System.Threading.Tasks;
using Extensibility;
using Fakes;
using NUnit.Framework;

[TestFixture]
public class BatchedMessageSessionTests
{
    [Test]
    public async Task Send_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using IBatchedMessageSession session = new BatchedMessageSession(messageSession, new FakeDispatcher());

        await session.Send(new object());

        Assert.IsTrue(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public async Task Publish_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using IBatchedMessageSession session = new BatchedMessageSession(messageSession, new FakeDispatcher());

        await session.Publish(new object());

        Assert.IsTrue(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }
}