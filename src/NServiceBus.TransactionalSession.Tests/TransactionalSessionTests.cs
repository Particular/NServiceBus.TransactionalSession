namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Extensibility;
using Fakes;
using NUnit.Framework;

[TestFixture]
public class TransactionalSessionTests
{
    [Test]
    public async Task Open_should_generate_new_session_id()
    {
        using IBatchSession session = new TransactionalSession(new FakeMessageSession(), new FakeDispatcher());

        Assert.IsNull(session.SessionId);

        await session.Open();

        Assert.NotNull(session.SessionId);
    }

    [Test]
    public async Task Open_should_use_session_id_from_options()
    {
        using IBatchSession session = new TransactionalSession(new FakeMessageSession(), new FakeDispatcher());

        var openOptions = new OpenSessionOptions();
        await session.Open(openOptions);

        Assert.AreEqual(openOptions.SessionId, session.SessionId);
    }

    [Test]
    public async Task Open_should_throw_if_session_already_open()
    {
        using IBatchSession session = new TransactionalSession(new FakeMessageSession(), new FakeDispatcher());

        await session.Open();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open());

        StringAssert.Contains($"This session is already open. Open should only be called once.", exception.Message);
    }

    [Test]
    public async Task Send_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using IBatchSession session = new TransactionalSession(messageSession, new FakeDispatcher());

        await session.Open();
        await session.Send(new object());

        Assert.IsTrue(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public async Task Publish_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using IBatchSession session = new TransactionalSession(messageSession, new FakeDispatcher());

        await session.Open();
        await session.Publish(new object());

        Assert.IsTrue(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public void Send_should_throw_exeception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using IBatchSession session = new TransactionalSession(new FakeMessageSession(), new FakeDispatcher());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

        StringAssert.Contains("Before sending any messages, make sure to open the session by calling the `Open`-method.", exception.Message);
        Assert.IsEmpty(messageSession.SentMessages);
    }

    [Test]
    public void Publish_should_throw_exception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using IBatchSession session = new TransactionalSession(new FakeMessageSession(), new FakeDispatcher());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

        StringAssert.Contains("Before publishing any messages, make sure to open the session by calling the `Open`-method.", exception.Message);
        Assert.IsEmpty(messageSession.PublishedMessages);
    }
}