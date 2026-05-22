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
using Transport;

[TestFixture]
public class OutboxTransactionalSessionTests
{
    static OutboxTransactionalSession CreateSession(
        FakeOutboxStorage outboxStorage = null,
        FakeSynchronizableStorageSession synchronizedStorageSession = null,
        FakeMessageSession messageSession = null,
        FakeDispatcher dispatcher = null,
        string queueAddress = "queue address",
        bool isSendOnly = false,
        TransactionalSessionMetrics metrics = null)
    {
        return new OutboxTransactionalSession(
            outboxStorage ?? new FakeOutboxStorage(),
            synchronizedStorageSession ?? new FakeSynchronizableStorageSession(),
            messageSession ?? new FakeMessageSession(),
            dispatcher ?? new FakeDispatcher(),
            [],
            queueAddress,
            isSendOnly,
            metrics ?? new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"),
            new EndpointLoggingScope { EndpointName = "endpointName" },
            new FakeLogger<OutboxTransactionalSession>());
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
    public async Task Open_should_open_outbox_synchronized_storage_session()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();
        var outboxStorage = new FakeOutboxStorage();

        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: synchronizedStorageSession);

        await session.Open(new FakeOpenSessionOptions());

        Assert.Multiple(() =>
        {
            Assert.That(outboxStorage.StartedTransactions.Single(), Is.SameAs(synchronizedStorageSession.OpenedOutboxTransactionSessions.Single().Item1));
            Assert.That(session.SynchronizedStorageSession, Is.EqualTo(synchronizedStorageSession));
        });
    }

    [Test]
    public void Open_should_throw_exception_when_storage_session_not_compatible_with_outbox()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();
        synchronizedStorageSession.TryOpenCallback = (_, _) => false;

        using var session = CreateSession(synchronizedStorageSession: synchronizedStorageSession);

        var exception = Assert.ThrowsAsync<Exception>(async () => await session.Open(new FakeOpenSessionOptions()));

        Assert.That(exception.Message, Is.EqualTo("Outbox and synchronized storage persister are not compatible."));
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
    public async Task Commit_should_send_control_message_and_store_outbox_data()
    {
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage();
        var synchronizedSession = new FakeSynchronizableStorageSession();
        const string queueAddress = "queue address";
        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: synchronizedSession, dispatcher: dispatcher, queueAddress: queueAddress);

        await session.Open(new FakeOpenSessionOptions());
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        await session.Send(new object(), sendOptions);
        await session.Commit();

        Assert.That(dispatcher.Dispatched, Has.Count.EqualTo(1), "should have dispatched control message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.That(dispatched.outgoingMessages.UnicastTransportOperations, Has.Count.EqualTo(1));
        var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(controlMessage.RequiredDispatchConsistency, Is.EqualTo(DispatchConsistency.Isolated));
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.TimeSentHeaderName], Is.Not.Empty);
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.AttemptHeaderName], Is.EqualTo("1"));
            Assert.That(controlMessage.Message.Body.IsEmpty, Is.True);
            Assert.That(controlMessage.Destination, Is.EqualTo(queueAddress));

            Assert.That(outboxStorage.Stored, Has.Count.EqualTo(1));
        });
        var outboxRecord = outboxStorage.Stored.Single();
        Assert.Multiple(() =>
        {
            Assert.That(outboxRecord.outboxMessage.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(outboxRecord.transaction, Is.EqualTo(outboxStorage.StartedTransactions.Single()));

            Assert.That(outboxRecord.outboxMessage.TransportOperations.Length, Is.EqualTo(1));
        });
        var outboxMessage = outboxRecord.outboxMessage.TransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(outboxMessage.MessageId, Is.EqualTo(messageId));

            Assert.That(synchronizedSession.Completed, Is.True);
            Assert.That(synchronizedSession.Disposed, Is.True);
            Assert.That(outboxStorage.StartedTransactions.Single().Committed, Is.True);
        });
    }

    [Test]
    public async Task Commit_should_emit_dispatch_metrics_when_need_to_send_messages()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizedSession = new FakeSynchronizableStorageSession();
        var meterFactory = new TestMeterFactory();
        var dispatchDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.dispatch.duration");
        const string endpointName = "endpointName";
        using var session = CreateSession(synchronizedStorageSession: synchronizedSession, dispatcher: dispatcher, metrics: new TransactionalSessionMetrics(meterFactory, endpointName));

        await session.Open(new FakeOpenSessionOptions());
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        await session.Send(new object(), sendOptions);
        await session.Commit();

        var dispatchDurationSnapshot = dispatchDuration.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(dispatchDurationSnapshot.Count, Is.EqualTo(1));
            Assert.That(dispatchDurationSnapshot[0].Value, Is.GreaterThan(0));
            Assert.That(dispatchDurationSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
        });
    }

    [Test]
    public async Task Commit_should_not_emit_dispatch_metrics_when_no_messages_to_send()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizedSession = new FakeSynchronizableStorageSession();
        var meterFactory = new TestMeterFactory();
        var dispatchDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.dispatch.duration");
        const string endpointName = "endpointName";
        using var session = CreateSession(synchronizedStorageSession: synchronizedSession, dispatcher: dispatcher, metrics: new TransactionalSessionMetrics(meterFactory, endpointName));

        await session.Open(new FakeOpenSessionOptions());
        await session.Commit();

        var dispatchDurationSnapshot = dispatchDuration.GetMeasurementSnapshot();
        Assert.That(dispatchDurationSnapshot.Count, Is.EqualTo(0));
    }

    [Test]
    public async Task Commit_should_emit_commit_metrics()
    {
        var dispatcher = new FakeDispatcher();
        var synchronizedSession = new FakeSynchronizableStorageSession();
        var meterFactory = new TestMeterFactory();
        var commitDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.commit.duration");
        const string endpointName = "endpointName";
        using var session = CreateSession(synchronizedStorageSession: synchronizedSession, dispatcher: dispatcher, metrics: new TransactionalSessionMetrics(meterFactory, endpointName));

        await session.Open(new FakeOpenSessionOptions());
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        await session.Send(new object(), sendOptions);
        await session.Commit();

        var commitDurationSnapshot = commitDuration.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(commitDurationSnapshot.Count, Is.EqualTo(1));
            Assert.That(commitDurationSnapshot[0].Value, Is.GreaterThan(0));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.commit.outcome"], Is.EqualTo("success"));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.mode"], Is.EqualTo("outbox"));
        });
    }

    [Test]
    public async Task Commit_should_not_send_control_message_when_there_are_no_outgoing_operations_and_not_store_the_outbox_record()
    {
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage();
        var synchronizedSession = new FakeSynchronizableStorageSession();

        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: synchronizedSession, dispatcher: dispatcher);

        await session.Open(new FakeOpenSessionOptions());
        // no outgoing operations
        await session.Commit();

        Assert.That(dispatcher.Dispatched, Is.Empty, "should not have dispatched control message");
        Assert.That(outboxStorage.Stored, Is.Empty, "should not have stored outbox record");
        Assert.Multiple(() =>
        {
            Assert.That(synchronizedSession.Completed, Is.True);
            Assert.That(synchronizedSession.Disposed, Is.True);
            Assert.That(outboxStorage.StartedTransactions.Single().Committed, Is.True);
            Assert.That(outboxStorage.Dispatched, Is.Empty, "should not have marked it as dispatched");
        });
    }

    [Test]
    public async Task Commit_should_send_control_message_when_outbox_fails()
    {
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
        var completableSynchronizedStorageSession = new FakeSynchronizableStorageSession();
        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: completableSynchronizedStorageSession, dispatcher: dispatcher);

        await session.Open(new FakeOpenSessionOptions());
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        Assert.That(dispatcher.Dispatched, Has.Count.EqualTo(1), "should have dispatched control message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.That(dispatched.outgoingMessages.UnicastTransportOperations, Has.Count.EqualTo(1));
        var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));
        });

        var outboxTransaction = outboxStorage.StartedTransactions.Single();
        Assert.That(outboxTransaction.Committed, Is.False, "should not have committed outbox operations");
    }

    [Test]
    public async Task Commit_should_emit_metrics_when_outbox_fails()
    {
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
        var completableSynchronizedStorageSession = new FakeSynchronizableStorageSession();
        var meterFactory = new TestMeterFactory();
        var commitDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.commit.duration");
        var dispatchDuration = new MetricCollector<double>(meterFactory,
            "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.dispatch.duration");
        const string endpointName = "endpointName";
        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: completableSynchronizedStorageSession, dispatcher: dispatcher, metrics: new TransactionalSessionMetrics(meterFactory, endpointName));

        await session.Open(new FakeOpenSessionOptions());
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        var commitDurationSnapshot = commitDuration.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(commitDurationSnapshot.Count, Is.EqualTo(1));
            Assert.That(commitDurationSnapshot[0].Value, Is.GreaterThan(0));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.commit.outcome"], Is.EqualTo("failure"));
            Assert.That(commitDurationSnapshot[0].Tags["nservicebus.transactional_session.mode"], Is.EqualTo("outbox"));
        });

        var dispatchDurationSnapshot = dispatchDuration.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(dispatchDurationSnapshot.Count, Is.EqualTo(1));
            Assert.That(dispatchDurationSnapshot[0].Value, Is.GreaterThan(0));
            Assert.That(dispatchDurationSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
        });
    }

    [Test]
    public async Task Commit_should_complete_synchronized_storage_session_before_outbox_store()
    {
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
        var completableSynchronizedStorageSession = new FakeSynchronizableStorageSession();
        using var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: completableSynchronizedStorageSession, dispatcher: dispatcher);

        await session.Open(new FakeOpenSessionOptions());
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        var outboxTransaction = outboxStorage.StartedTransactions.Single();
        Assert.Multiple(() =>
        {
            Assert.That(completableSynchronizedStorageSession.Completed, Is.True, "should have completed synchronized storage session to match the receive pipeline behavior");
            Assert.That(outboxTransaction.Committed, Is.False, "should not have committed outbox operations");
        });
    }

    [Test]
    public async Task Commit_should_append_open_metadata_to_control_message()
    {
        var expectedDelayIncrement = TimeSpan.FromSeconds(42);
        var expectedMaximumCommitDuration = TimeSpan.FromSeconds(1);
        const string expectedExtensionsValue = "extensions-value";
        const string expectedMetadataValue = "metadata-value";

        var dispatcher = new FakeDispatcher();
        using var session = CreateSession(dispatcher: dispatcher);

        var options = new FakeOpenSessionOptions
        {
            CommitDelayIncrement = expectedDelayIncrement,
            MaximumCommitDuration = expectedMaximumCommitDuration
        };
        options.Extensions.Set("extensions-key", expectedExtensionsValue);
        options.Metadata.Add("metadata-key", expectedMetadataValue);

        await session.Open(options);
        await session.Send(new object());
        await session.Commit();

        var controlMessage = dispatcher.Dispatched.Single().outgoingMessages.UnicastTransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName], Is.EqualTo(expectedDelayIncrement.ToString("c")));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName], Is.EqualTo(expectedMaximumCommitDuration.ToString("c")));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.TimeSentHeaderName], Is.Not.Empty);
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.AttemptHeaderName], Is.EqualTo("1"));
            Assert.That(controlMessage.Message.Headers["metadata-key"], Is.EqualTo(expectedMetadataValue), "metadata should be propagated to headers");
            Assert.That(controlMessage.Message.Headers.ContainsKey("extensions-key"), Is.False, "extensions should not be propagated to headers");
        });
    }

    [Test]
    public void Operations_should_throw_when_already_disposed()
    {
        ITransactionalSession session = CreateSession();

        session.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.Open(new FakeOpenSessionOptions()));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.Send(new object()));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.Publish(new object()));
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.Commit());

        Assert.DoesNotThrow(() => session.Dispose(), "multiple calls to dispose should not throw");
    }

    [Test]
    public async Task Operations_should_throw_when_already_committed()
    {
        ITransactionalSession session = CreateSession();

        await session.Open(new FakeOpenSessionOptions());
        await session.Commit();

        var openException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));
        Assert.That(openException.Message, Does.Contain("This session has already been committed. Complete all session operations before calling `Commit` or use a new session."));
        var sendException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));
        Assert.That(sendException.Message, Does.Contain("This session has already been committed. Complete all session operations before calling `Commit` or use a new session."));
        var publishException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));
        Assert.That(publishException.Message, Does.Contain("This session has already been committed. Complete all session operations before calling `Commit` or use a new session."));
        var commitException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Commit());
        Assert.That(commitException.Message, Does.Contain("This session has already been committed. Complete all session operations before calling `Commit` or use a new session."));
    }

    [Theory]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Dispose_should_dispose_synchronized_storage_and_outbox_transaction(bool async)
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();
        var outboxStorage = new FakeOutboxStorage();

        var session = CreateSession(outboxStorage: outboxStorage, synchronizedStorageSession: synchronizedStorageSession);
        await session.Open(new FakeOpenSessionOptions());

        if (async)
        {
            await session.DisposeAsync();
        }
        else
        {
            session.Dispose();
        }

        Assert.Multiple(() =>
        {
            Assert.That(outboxStorage.StartedTransactions.Single().Disposed, Is.True);
            Assert.That(synchronizedStorageSession.Disposed, Is.True);
        });
    }
}
