namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Extensibility;
using Fakes;
using Logging;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;

[TestFixture]
public class TransactionalSessionTests
{
    static NonOutboxTransactionalSession CreateSession(
        FakeSynchronizableStorageSession synchronizedStorageSession = null,
        FakeMessageSession messageSession = null,
        FakeDispatcher dispatcher = null,
        TransactionalSessionMetrics metrics = null)
    {
        return new NonOutboxTransactionalSession(
            synchronizedStorageSession ?? new FakeSynchronizableStorageSession(),
            messageSession ?? new FakeMessageSession(),
            dispatcher ?? new FakeDispatcher(),
            [],
            metrics ?? new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"),
            new EndpointLoggingScope { EndpointName = "endpointName" },
            new FakeLogger<NonOutboxTransactionalSession>());
    }

    [Test]
    public async Task Open_should_use_session_id_from_options()
    {
        using var session = CreateSession();

        var openOptions = new FakeOpenSessionOptions();
        await session.Open(openOptions);

        Assert.That(session.SessionId, Is.EqualTo(openOptions.SessionId));
    }

    [Test]
    public async Task Open_should_throw_if_session_already_open()
    {
        using var session = CreateSession();

        await session.Open(new FakeOpenSessionOptions());

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));

        Assert.That(exception.Message, Does.Contain($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once."));
    }

    [Test]
    public async Task Open_should_open_synchronized_storage_session()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();

        using var session = CreateSession(synchronizedStorageSession: synchronizedStorageSession);

        var options = new FakeOpenSessionOptions();
        await session.Open(options);

        Assert.Multiple(() =>
        {
            Assert.That(synchronizedStorageSession.OpenedOutboxTransactionSessions, Is.Empty);
            Assert.That(synchronizedStorageSession.OpenedTransactionSessions, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(synchronizedStorageSession.OpenedTransactionSessions.Single(), Is.EqualTo(options.Extensions));
            Assert.That(session.SynchronizedStorageSession, Is.EqualTo(synchronizedStorageSession));
        });
    }

    [Test]
    public async Task Send_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = CreateSession(messageSession: messageSession);

        await session.Open(new FakeOpenSessionOptions());
        await session.Send(new object());

        Assert.That(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
    }

    [Test]
    public async Task Publish_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = CreateSession(messageSession: messageSession);

        await session.Open(new FakeOpenSessionOptions());
        await session.Publish(new object());

        Assert.That(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
    }

    [Test]
    public void Send_should_throw_exception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = CreateSession(messageSession: messageSession);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

        Assert.That(exception.Message, Does.Contain("This session has not been opened yet."));
        Assert.That(messageSession.SentMessages, Is.Empty);
    }

    [Test]
    public void Publish_should_throw_exception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = CreateSession(messageSession: messageSession);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

        Assert.That(exception.Message, Does.Contain("This session has not been opened yet."));
        Assert.That(messageSession.PublishedMessages, Is.Empty);
    }

    [Test]
    public async Task Commit_should_send_message_and_commit_storage_tx()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizableSession = new FakeSynchronizableStorageSession();
        using var session = CreateSession(synchronizedStorageSession: synchronizableSession, dispatcher: dispatcher);

        await session.Open(new FakeOpenSessionOptions());
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        var messageObj = new object();
        await session.Send(messageObj, sendOptions);
        await session.Commit();

        Assert.That(dispatcher.Dispatched, Has.Count.EqualTo(1), "should have dispatched message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.That(dispatched.outgoingMessages.UnicastTransportOperations, Has.Count.EqualTo(1));
        var dispatchedMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(dispatchedMessage.Message.MessageId, Is.EqualTo(messageId));
            Assert.That(dispatchedMessage.Message.Headers.ContainsKey(Headers.ControlMessageHeader), Is.False);

            Assert.That(synchronizableSession.Completed, Is.True);
        });
    }

    [Test]
    public async Task Commit_should_emit_metrics()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizableSession = new FakeSynchronizableStorageSession();
        var meterFactory = new TestMeterFactory();
        var commitDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.commit.duration");
        const string endpointName = "endpointName";
        using var session = CreateSession(synchronizedStorageSession: synchronizableSession, dispatcher: dispatcher, metrics: new TransactionalSessionMetrics(meterFactory, endpointName));

        await session.Open(new FakeOpenSessionOptions());
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        var messageObj = new object();
        await session.Send(messageObj, sendOptions);
        await session.Commit();

        var commitDurationSnapshot = commitDuration.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(commitDurationSnapshot.Count, Is.EqualTo(1));
            Assert.That(commitDurationSnapshot[0].Value, Is.GreaterThan(0));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.commit.outcome"], Is.EqualTo("success"));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.mode"], Is.EqualTo("non_outbox"));
        });
    }

    [Test]
    public async Task Commit_should_not_send_message_when_storage_tx_fails()
    {
        var dispatcher = new FakeDispatcher();
        var storageSession = new FakeSynchronizableStorageSession();
        storageSession.CompleteCallback = () => throw new Exception("session complete exception");

        using var session = CreateSession(synchronizedStorageSession: storageSession, dispatcher: dispatcher);

        await session.Open(new FakeOpenSessionOptions());
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        Assert.That(dispatcher.Dispatched, Is.Empty, "should not have dispatched message");
    }

    [Theory]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Dispose_should_dispose_synchronized_storage_session(bool async)
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();

        var session = CreateSession(synchronizedStorageSession: synchronizedStorageSession);
        var options = new FakeOpenSessionOptions();
        await session.Open(options);

        if (async)
        {
            await session.DisposeAsync();
        }
        else
        {
            session.Dispose();
        }

        Assert.That(synchronizedStorageSession.Disposed, Is.True);
    }

    [Test]
    public async Task Open_should_clean_up_logging_scope_when_open_fails()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession
        {
            OpenCallback = async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException("open failed");
            }
        };

        var session = new NonOutboxTransactionalSession(
            synchronizedStorageSession,
            new FakeMessageSession(),
            new FakeDispatcher(),
            [],
            new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"),
            new EndpointLoggingScope { EndpointName = "endpointName" },
            new FakeLogger<NonOutboxTransactionalSession>());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));

        Assert.DoesNotThrow(() => session.Dispose());
    }
}
