namespace NServiceBus.TransactionalSession.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Fakes;
    using NUnit.Framework;

    [TestFixture]
    public class OutboxTransactionalSessionTests
    {
        [Test]
        public async Task Open_should_use_session_id_from_options()
        {
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var openOptions = new FakeOpenSessionOptions();
            await session.Open(openOptions);

            Assert.That(session.SessionId, Is.EqualTo(openOptions.SessionId));
        }

        [Test]
        public async Task Open_should_throw_if_session_already_open()
        {
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));

            StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
        }

        [Test]
        public async Task Open_should_open_outbox_synchronized_storage_session()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();
            var outboxStorage = new FakeOutboxStorage();

            using var session = new OutboxTransactionalSession(outboxStorage, synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());

            Assert.AreSame(synchronizedStorageSession.OpenedOutboxTransactionSessions.Single().Item1, outboxStorage.StartedTransactions.Single());
            Assert.That(session.SynchronizedStorageSession, Is.EqualTo(synchronizedStorageSession));
        }

        [Test]
        public void Open_should_throw_exception_when_storage_session_not_compatible_with_outbox()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();
            synchronizedStorageSession.TryOpenCallback = (_, _) => false;

            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<Exception>(async () => await session.Open(new FakeOpenSessionOptions()));

            Assert.That(exception.Message, Is.EqualTo("Outbox and synchronized storage persister are not compatible."));
        }

        [Test]
        public async Task Send_should_set_PendingOperations_collection_on_context()
        {
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());

            Assert.That(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
        }

        [Test]
        public async Task Publish_should_set_PendingOperations_collection_on_context()
        {
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Publish(new object());

            Assert.That(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
        }

        [Test]
        public void Send_should_throw_exception_when_session_not_opened()
        {
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.IsEmpty(messageSession.SentMessages);
        }

        [Test]
        public void Publish_should_throw_exception_when_session_not_opened()
        {
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.IsEmpty(messageSession.PublishedMessages);
        }

        [Test]
        public async Task Commit_should_send_control_message_and_store_outbox_data()
        {
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage();
            var synchronizedSession = new FakeSynchronizableStorageSession();
            string queueAddress = "queue address";

            using var session = new OutboxTransactionalSession(outboxStorage, synchronizedSession, messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), queueAddress);

            await session.Open(new FakeOpenSessionOptions());
            var sendOptions = new SendOptions();
            string messageId = Guid.NewGuid().ToString();
            sendOptions.SetMessageId(messageId);
            await session.Send(new object(), sendOptions);
            await session.Commit();

            Assert.That(dispatcher.Dispatched.Count, Is.EqualTo(1), "should have dispatched control message");
            var dispatched = dispatcher.Dispatched.Single();
            Assert.That(dispatched.outgoingMessages.UnicastTransportOperations.Count, Is.EqualTo(1));
            var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));
            Assert.That(controlMessage.Message.Body.IsEmpty, Is.True);
            Assert.That(controlMessage.Destination, Is.EqualTo(queueAddress));

            Assert.That(outboxStorage.Stored.Count, Is.EqualTo(1));
            var outboxRecord = outboxStorage.Stored.Single();
            Assert.That(outboxRecord.outboxMessage.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(outboxRecord.transaction, Is.EqualTo(outboxStorage.StartedTransactions.Single()));

            Assert.That(outboxRecord.outboxMessage.TransportOperations.Length, Is.EqualTo(1));
            var outboxMessage = outboxRecord.outboxMessage.TransportOperations.Single();
            Assert.That(outboxMessage.MessageId, Is.EqualTo(messageId));

            Assert.That(synchronizedSession.Completed, Is.True);
            Assert.That(synchronizedSession.Disposed, Is.True);
            Assert.That(outboxStorage.StartedTransactions.Single().Committed, Is.True);
        }

        [Test]
        public async Task Commit_should_send_control_message_when_outbox_fails()
        {
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
            var completableSynchronizedStorageSession = new FakeSynchronizableStorageSession();
            using var session = new OutboxTransactionalSession(outboxStorage, completableSynchronizedStorageSession, messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());
            Assert.ThrowsAsync<Exception>(async () => await session.Commit());

            Assert.That(dispatcher.Dispatched.Count, Is.EqualTo(1), "should have dispatched control message");
            var dispatched = dispatcher.Dispatched.Single();
            Assert.That(dispatched.outgoingMessages.UnicastTransportOperations.Count, Is.EqualTo(1));
            var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));

            var outboxTransaction = outboxStorage.StartedTransactions.Single();
            Assert.That(outboxTransaction.Committed, Is.False, "should not have committed outbox operations");
        }

        [Test]
        public async Task Commit_should_complete_synchronized_storage_session_before_outbox_store()
        {
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
            var completableSynchronizedStorageSession = new FakeSynchronizableStorageSession();
            using var session = new OutboxTransactionalSession(outboxStorage, completableSynchronizedStorageSession, messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());
            Assert.ThrowsAsync<Exception>(async () => await session.Commit());

            var outboxTransaction = outboxStorage.StartedTransactions.Single();
            Assert.That(completableSynchronizedStorageSession.Completed, Is.True, "should have completed synchronized storage session to match the receive pipeline behavior");
            Assert.That(outboxTransaction.Committed, Is.False, "should not have committed outbox operations");
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
            using var session = new OutboxTransactionalSession(outboxStorage, new FakeSynchronizableStorageSession(), messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var options = new FakeOpenSessionOptions();
            options.CommitDelayIncrement = expectedDelayIncrement;
            options.MaximumCommitDuration = expectedMaximumCommitDuration;
            options.Extensions.Set("extensions-key", expectedExtensionsValue);
            options.Metadata.Add("metadata-key", expectedMetadataValue);

            await session.Open(options);
            await session.Commit();

            var controlMessage = dispatcher.Dispatched.Single().outgoingMessages.UnicastTransportOperations.Single();
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(session.SessionId));
            Assert.That(controlMessage.Message.Headers[Headers.ControlMessageHeader], Is.EqualTo(bool.TrueString));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName], Is.EqualTo(expectedDelayIncrement.ToString("c")));
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName], Is.EqualTo(expectedMaximumCommitDuration.ToString("c")));
            Assert.That(controlMessage.Message.Headers["metadata-key"], Is.EqualTo(expectedMetadataValue), "metadata should be propagated to headers");
            Assert.That(controlMessage.Message.Headers.ContainsKey("extensions-key"), Is.False, "extensions should not be propagated to headers");
        }

        [Test]
        public void Operations_should_throw_when_already_disposed()
        {
            ITransactionalSession session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

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
            ITransactionalSession session = new OutboxTransactionalSession(new FakeOutboxStorage(), new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Commit();

            var openException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));
            StringAssert.Contains("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.", openException.Message);
            var sendException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));
            StringAssert.Contains("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.", sendException.Message);
            var publishException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));
            StringAssert.Contains("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.", publishException.Message);
            var commitException = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Commit());
            StringAssert.Contains("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.", commitException.Message);
        }

        [Test]
        public async Task Dispose_should_dispose_synchronized_storage_and_outbox_transaction()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();
            var outboxStorage = new FakeOutboxStorage();

            var session = new OutboxTransactionalSession(outboxStorage, synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");
            await session.Open(new FakeOpenSessionOptions());

            session.Dispose();

            Assert.That(outboxStorage.StartedTransactions.Single().Disposed, Is.True);
            Assert.That(synchronizedStorageSession.Disposed, Is.True);
        }
    }
}