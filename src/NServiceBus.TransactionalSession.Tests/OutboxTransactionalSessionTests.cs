namespace NServiceBus.TransactionalSession.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Fakes;
    using NUnit.Framework;
    using Persistence;

    [TestFixture]
    public class OutboxTransactionalSessionTests
    {
        [Test]
        public async Task Open_should_use_session_id_from_options()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var openOptions = new FakeOpenSessionOptions();
            await session.Open(openOptions);

            Assert.AreEqual(openOptions.SessionId, session.SessionId);
        }

        [Test]
        public async Task Open_should_throw_if_session_already_open()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));

            StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
        }

        [Test]
        public async Task Open_should_open_outbox_synchronized_storage_session()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var outboxStorage = new FakeOutboxStorage();

            using var session = new OutboxTransactionalSession(outboxStorage, new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());

            Assert.AreSame(fakeSynchronizedStorage.OpenedOutboxTransactionSessions.Single().Item1, outboxStorage.StartedTransactions.Single());
            Assert.AreEqual(fakeSynchronizedStorage.Session, session.SynchronizedStorageSession);
        }

        [Test]
        public void Open_should_throw_exception_when_storage_session_not_compatible_with_outbox()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage
            {
                TryOpenCallback = (_, _) => null
            };

            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<Exception>(async () => await session.Open(new FakeOpenSessionOptions()));

            Assert.AreEqual("Outbox and synchronized storage persister are not compatible.", exception.Message);
        }

        [Test]
        public async Task Send_should_set_PendingOperations_collection_on_context()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());

            Assert.IsTrue(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
        }

        [Test]
        public async Task Publish_should_set_PendingOperations_collection_on_context()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Publish(new object());

            Assert.IsTrue(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations));
        }

        [Test]
        public void Send_should_throw_exeception_when_session_not_opened()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.IsEmpty(messageSession.SentMessages);
        }

        [Test]
        public void Publish_should_throw_exception_when_session_not_opened()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            using var session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.IsEmpty(messageSession.PublishedMessages);
        }

        [Test]
        public async Task Commit_should_send_control_message_and_store_outbox_data()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage();
            string queueAddress = "queue address";

            using var session = new OutboxTransactionalSession(outboxStorage, new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), queueAddress);

            await session.Open(new FakeOpenSessionOptions());
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
            Assert.IsTrue(controlMessage.Message.Body.Length == 0);
            Assert.AreEqual(queueAddress, controlMessage.Destination);

            Assert.AreEqual(1, outboxStorage.Stored.Count);
            var outboxRecord = outboxStorage.Stored.Single();
            Assert.AreEqual(session.SessionId, outboxRecord.outboxMessage.MessageId);
            Assert.AreEqual(outboxStorage.StartedTransactions.Single(), outboxRecord.transaction);

            Assert.AreEqual(1, outboxRecord.outboxMessage.TransportOperations.Length);
            var outboxMessage = outboxRecord.outboxMessage.TransportOperations.Single();
            Assert.AreEqual(messageId, outboxMessage.MessageId);

            Assert.IsTrue(fakeSynchronizedStorage.Completed);
            Assert.IsTrue(outboxStorage.StartedTransactions.Single().Commited);
        }

        [Test]
        public async Task Commit_should_send_control_message_when_outbox_fails()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
            using var session = new OutboxTransactionalSession(outboxStorage, new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());
            Assert.ThrowsAsync<Exception>(async () => await session.Commit());

            Assert.AreEqual(1, dispatcher.Dispatched.Count, "should have dispatched control message");
            var dispatched = dispatcher.Dispatched.Single();
            Assert.AreEqual(1, dispatched.outgoingMessages.UnicastTransportOperations.Count);
            var controlMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
            Assert.AreEqual(session.SessionId, controlMessage.Message.MessageId);
            Assert.AreEqual(bool.TrueString, controlMessage.Message.Headers[Headers.ControlMessageHeader]);

            var outboxTransaction = outboxStorage.StartedTransactions.Single();
            Assert.IsFalse(outboxTransaction.Commited, "should not have committed outbox operations");
        }

        [Test]
        public async Task Commit_should_complete_synchronized_storage_session_before_outbox_store()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage { StoreCallback = (_, _, _) => throw new Exception("some error") };
            using var session = new OutboxTransactionalSession(outboxStorage, new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());
            Assert.ThrowsAsync<Exception>(async () => await session.Commit());

            var outboxTransaction = outboxStorage.StartedTransactions.Single();
            Assert.IsTrue(fakeSynchronizedStorage.Completed, "should have completed synchronized storage session to match the receive pipeline behavior");
            Assert.IsFalse(outboxTransaction.Commited, "should not have committed outbox operations");
        }

        [Test]
        public async Task Commit_should_append_open_metadata_to_control_message()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            var expectedDelayIncrement = TimeSpan.FromSeconds(42);
            var expectedMaximumCommitDuration = TimeSpan.FromSeconds(1);
            const string expectedExtensionsValue = "extensions-value";
            const string expectedMetadataValue = "metadata-value";

            var messageSession = new FakeMessageSession();
            var dispatcher = new FakeDispatcher();
            var outboxStorage = new FakeOutboxStorage();
            using var session = new OutboxTransactionalSession(outboxStorage, new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), messageSession, dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

            var options = new FakeOpenSessionOptions();
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

        [Test]
        public void Operations_should_throw_when_already_disposed()
        {
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            ITransactionalSession session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

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
            var fakeSynchronizedStorage = new FakeSynchronizedStorage();
            ITransactionalSession session = new OutboxTransactionalSession(new FakeOutboxStorage(), new CompletableSynchronizedStorageSessionAdapter(fakeSynchronizedStorage, fakeSynchronizedStorage), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>(), "queue address");

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
    }
}