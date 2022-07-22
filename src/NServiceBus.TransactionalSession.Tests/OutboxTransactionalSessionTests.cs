namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using NUnit.Framework;
using Outbox;
using Persistence;
using Routing;
using Testing;
using Transport;
using TransportOperation = Transport.TransportOperation;

[TestFixture]
public class OutboxTransactionalSessionTests
{
    [Test]
    public async Task Open_should_generate_new_session_id()
    {
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), "queue address");

        Assert.IsNull(session.SessionId);

        await session.Open();

        Assert.NotNull(session.SessionId);
    }

    [Test]
    public async Task Open_should_throw_if_session_already_open()
    {
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), "queue address");

        await session.Open();

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open());

        StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
    }

    [Test]
    public async Task Open_should_open_outbox_synchronized_storage_session()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();
        var outboxStorage = new FakeOutboxStorage();

        using var session = new OutboxTransactionalSession(outboxStorage, synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), "queue address");

        await session.Open();

        Assert.AreSame(synchronizedStorageSession.OpenedOutboxTransactionSessions.Single().Item1, outboxStorage.StartedTransactions.Single());
        Assert.AreEqual(synchronizedStorageSession, session.SynchronizedStorageSession);
    }

    [Test]
    public void Open_should_throw_exception_when_storage_session_not_compatible_with_outbox()
    {
        var synchronizedStorageSession = new FakeSynchronizableStorageSession();
        synchronizedStorageSession.TryOpenCallback = (_, _) => false;

        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), "queue address");

        var exception = Assert.ThrowsAsync<Exception>(async () => await session.Open());

        Assert.AreEqual("Outbox and synchronized storage persister are not compatible.", exception.Message);
    }

    [Test]
    public async Task Send_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), "queue address");

        await session.Open();
        await session.Send(new object());

        Assert.IsTrue(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public async Task Publish_should_set_PendingOperations_collection_on_context()
    {
        var messageSession = new FakeMessageSession();
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), "queue address");

        await session.Open();
        await session.Publish(new object());

        Assert.IsTrue(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
    }

    [Test]
    public void Send_should_throw_exeception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), "queue address");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

        StringAssert.Contains("Before sending any messages, make sure to open the session by calling the `Open`-method.", exception.Message);
        Assert.IsEmpty(messageSession.SentMessages);
    }

    [Test]
    public void Publish_should_throw_exception_when_session_not_opened()
    {
        var messageSession = new FakeMessageSession();
        using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), "queue address");

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

        StringAssert.Contains("Before publishing any messages, make sure to open the session by calling the `Open`-method.", exception.Message);
        Assert.IsEmpty(messageSession.PublishedMessages);
    }

    [Test]
    public async Task Commit_should_send_control_message_and_store_outbox_data()
    {
        var messageSession = new FakeMessageSession();
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage();
        string queueAddress = "queue address";
        using var session = new OutboxTransactionalSession(outboxStorage, new FakeSynchronizableStorageSession(), messageSession, dispatcher, queueAddress);

        await session.Open();
        var sendOptions = new SendOptions();
        string messageId = Guid.NewGuid().ToString();
        sendOptions.SetMessageId(messageId);
        await session.Send(new object(), sendOptions);
        await session.Commit();

        Assert.AreEqual(1, dispatcher.Dispatched.Count, "should have dispatched control message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.AreEqual(1, dispatched.outgoingMessages.UnicastTransportOperations.Count);
        var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.AreEqual(session.SessionId, controlMessage.Message.MessageId);
        Assert.AreEqual(bool.TrueString, controlMessage.Message.Headers[Headers.ControlMessageHeader]);
        Assert.IsTrue(controlMessage.Message.Body.IsEmpty);
        Assert.AreEqual(queueAddress, controlMessage.Destination);

        Assert.AreEqual(1, outboxStorage.Stored.Count);
        var outboxRecord = outboxStorage.Stored.Single();
        Assert.AreEqual(session.SessionId, outboxRecord.outboxMessage.MessageId);
        Assert.AreEqual(outboxStorage.StartedTransactions.Single(), outboxRecord.transaction);

        Assert.AreEqual(1, outboxRecord.outboxMessage.TransportOperations.Length);
        var outboxMessage = outboxRecord.outboxMessage.TransportOperations.Single();
        Assert.AreEqual(messageId, outboxMessage.MessageId);

        Assert.IsTrue(outboxStorage.StartedTransactions.Single().Commited);
    }

    [Test]
    public async Task Commit_should_send_control_message_when_outbox_fails()
    {
        var messageSession = new FakeMessageSession();
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
        using var session = new OutboxTransactionalSession(outboxStorage, new FakeSynchronizableStorageSession(), messageSession, dispatcher, "queue address");

        await session.Open();
        await session.Send(new object());
        Assert.ThrowsAsync<Exception>(async () => await session.Commit());

        Assert.AreEqual(1, dispatcher.Dispatched.Count, "should have dispatched control message");
        var dispatched = dispatcher.Dispatched.Single();
        Assert.AreEqual(1, dispatched.outgoingMessages.UnicastTransportOperations.Count);
        var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
        Assert.AreEqual(session.SessionId, controlMessage.Message.MessageId);
        Assert.AreEqual(bool.TrueString, controlMessage.Message.Headers[Headers.ControlMessageHeader]);

        var outboxTransaction = outboxStorage.StartedTransactions.Single();
        Assert.IsFalse(outboxTransaction.Commited, "should not have commited outbox operations");
    }

    [Test]
    public async Task Commit_should_append_open_metadata_to_control_message()
    {
        var expectedDelayIncrement = TimeSpan.FromSeconds(42);
        var expectedMaximumCommitDuration = TimeSpan.FromSeconds(1);
        const string expectedExtensionsValue = "extensions-value";
        const string expectedMetadataValue = "metadata-value";

        var messageSession = new FakeMessageSession();
        var dispatcher = new FakeDispatcher();
        var outboxStorage = new FakeOutboxStorage();
        using var session = new OutboxTransactionalSession(outboxStorage, new FakeSynchronizableStorageSession(), messageSession, dispatcher, "queue address");

        var options = new OpenSessionOptions();
        options.CommitDelayIncrement = expectedDelayIncrement;
        options.MaximumCommitDuration = expectedMaximumCommitDuration;
        options.Extensions.Set("extensions-key", expectedExtensionsValue);
        options.Metadata.Add("metadata-key", expectedMetadataValue);

        await session.Open(options);
        await session.Commit();

        var controlMessage = dispatcher.Dispatched.Single().outgoingMessages.UnicastTransportOperations.Single();
        Assert.AreEqual(session.SessionId, controlMessage.Message.MessageId);
        Assert.AreEqual(bool.TrueString, controlMessage.Message.Headers[Headers.ControlMessageHeader]);
        Assert.AreEqual(expectedDelayIncrement.ToString("c"), controlMessage.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName]);
        Assert.AreEqual(expectedMaximumCommitDuration.ToString("c"), controlMessage.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName]);
        Assert.AreEqual(expectedMetadataValue, controlMessage.Message.Headers["metadata-key"], "metadata should be propagated to headers");
        Assert.IsFalse(controlMessage.Message.Headers.ContainsKey("extensions-key"), "extensions should not be propagated to headers");
    }

    class FakeDispatcher : IMessageDispatcher
    {
        public List<(TransportOperations outgoingMessages, TransportTransaction transaction)> Dispatched = new();

        public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Dispatched.Add((outgoingMessages, transaction));
            return Task.CompletedTask;
        }
    }

    class FakeSynchronizableStorageSession : ICompletableSynchronizedStorageSession
    {
        public List<(IOutboxTransaction, ContextBag)> OpenedOutboxTransactionSessions { get; } = new();
        public Func<IOutboxTransaction, ContextBag, bool> TryOpenCallback { get; set; } = null;

        public void Dispose() { }

        public ValueTask<bool> TryOpen(IOutboxTransaction transaction, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            OpenedOutboxTransactionSessions.Add((transaction, context));
            return new ValueTask<bool>(TryOpenCallback?.Invoke(transaction, context) ?? true);
        }

        public ValueTask<bool> TryOpen(TransportTransaction transportTransaction, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken()) =>
            throw new InvalidOperationException("TransactionalSession must not adapt a transport transaction");

        public Task Open(ContextBag contextBag, CancellationToken cancellationToken = new CancellationToken()) => throw new System.NotImplementedException();

        public Task CompleteAsync(CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;
    }

    class FakeOutboxStorage : IOutboxStorage
    {
        public List<(OutboxMessage outboxMessage, IOutboxTransaction transaction, ContextBag context)> Stored { get; } = new();
        public Action<OutboxMessage, IOutboxTransaction, ContextBag> StoreCallback { get; set; } = null;

        public List<FakeOutboxTransaction> StartedTransactions { get; } = new();

        public Task<OutboxMessage> Get(string messageId, ContextBag context, CancellationToken cancellationToken = new CancellationToken()) => throw new System.NotImplementedException();

        public Task Store(OutboxMessage message, IOutboxTransaction transaction, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken())
        {
            Stored.Add((message, transaction, context));
            StoreCallback?.Invoke(message, transaction, context);

            return Task.CompletedTask;
        }

        public Task SetAsDispatched(string messageId, ContextBag context,
            CancellationToken cancellationToken = new CancellationToken()) =>
            throw new System.NotImplementedException();

        public Task<IOutboxTransaction> BeginTransaction(ContextBag context, CancellationToken cancellationToken = new CancellationToken())
        {
            var tx = new FakeOutboxTransaction();
            StartedTransactions.Add(tx);
            return Task.FromResult<IOutboxTransaction>(tx);
        }
    }

    class FakeOutboxTransaction : IOutboxTransaction
    {
        public bool Commited { get; private set; }

        public void Dispose() { }

        public Task Commit(CancellationToken cancellationToken = new CancellationToken())
        {
            Commited = true;
            return Task.CompletedTask;
        }
    }

    class FakeMessageSession : TestableMessageSession
    {
        public override Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(sendOptions);
            return base.Send(message, sendOptions, cancellationToken);
        }

        public override Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(sendOptions);
            return base.Send(messageConstructor, sendOptions, cancellationToken);
        }

        public override Task Publish(object message, PublishOptions publishOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(publishOptions);
            return base.Publish(message, publishOptions, cancellationToken);
        }

        public override Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(publishOptions);
            return base.Publish(messageConstructor, publishOptions, cancellationToken);
        }

        static void AddToPendingOperations(ExtendableOptions sendOptions)
        {
            // we need to fake the pipeline behavior to add outgoing messages to the PendingTransportOperations for the TransactionalSession to work.
            var pendingOperations = sendOptions.GetExtensions().Get<PendingTransportOperations>();
            pendingOperations.Add(new TransportOperation(
                new OutgoingMessage(sendOptions.GetMessageId() ?? Guid.NewGuid().ToString(),
                    sendOptions.GetHeaders().ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Encoding.UTF8.GetBytes("fake body")),
                new UnicastAddressTag("fake address")));
        }
    }
}