namespace NServiceBus.TransactionalSession.Tests
{
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
        public async Task Open_should_use_session_id_from_options()
        {
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var openOptions = new FakeOpenSessionOptions();
            await session.Open(openOptions);

            Assert.That(session.SessionId, Is.EqualTo(openOptions.SessionId));
        }

        [Test]
        public async Task Open_should_throw_if_session_already_open()
        {
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new FakeOpenSessionOptions());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new FakeOpenSessionOptions()));

            StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
        }

        [Test]
        public async Task Open_should_open_synchronized_storage_session()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            using var session = new NonOutboxTransactionalSession(synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var options = new FakeOpenSessionOptions();
            await session.Open(options);

            Assert.That(synchronizedStorageSession.OpenedOutboxTransactionSessions, Is.Empty);
            Assert.That(synchronizedStorageSession.OpenedTransactionSessions.Count, Is.EqualTo(1));
            Assert.That(synchronizedStorageSession.OpenedTransactionSessions.Single(), Is.EqualTo(options.Extensions));
            Assert.That(session.SynchronizedStorageSession, Is.EqualTo(synchronizedStorageSession));
        }

        [Test]
        public async Task Send_should_set_PendingOperations_collection_on_context()
        {
            var messageSession = new FakeMessageSession();
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());

            Assert.That(messageSession.SentMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
        }

        [Test]
        public async Task Publish_should_set_PendingOperations_collection_on_context()
        {
            var messageSession = new FakeMessageSession();
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new FakeOpenSessionOptions());
            await session.Publish(new object());

            Assert.That(messageSession.PublishedMessages.Single().Options.GetExtensions().TryGet(out PendingTransportOperations pendingTransportOperations), Is.True);
        }

        [Test]
        public void Send_should_throw_exception_when_session_not_opened()
        {
            var messageSession = new FakeMessageSession();
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.That(messageSession.SentMessages, Is.Empty);
        }

        [Test]
        public void Publish_should_throw_exception_when_session_not_opened()
        {
            var messageSession = new FakeMessageSession();
            using var session = new NonOutboxTransactionalSession(new FakeSynchronizableStorageSession(), messageSession, new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
            Assert.That(messageSession.PublishedMessages, Is.Empty);
        }

        [Test]
        public async Task Commit_should_send_message_and_commit_storage_tx()
        {
            var dispatcher = new FakeDispatcher();
            var synchronizableSession = new FakeSynchronizableStorageSession();
            using var session = new NonOutboxTransactionalSession(synchronizableSession, new FakeMessageSession(), dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new FakeOpenSessionOptions());
            var sendOptions = new SendOptions();
            string messageId = Guid.NewGuid().ToString();
            sendOptions.SetMessageId(messageId);
            var messageObj = new object();
            await session.Send(messageObj, sendOptions);
            await session.Commit();

            Assert.That(dispatcher.Dispatched.Count, Is.EqualTo(1), "should have dispatched message");
            var dispatched = dispatcher.Dispatched.Single();
            Assert.That(dispatched.outgoingMessages.UnicastTransportOperations.Count, Is.EqualTo(1));
            var dispatchedMessage = dispatched.outgoingMessages.UnicastTransportOperations.Single();
            Assert.That(dispatchedMessage.Message.MessageId, Is.EqualTo(messageId));
            Assert.That(dispatchedMessage.Message.Headers.ContainsKey(Headers.ControlMessageHeader), Is.False);

            Assert.That(synchronizableSession.Completed, Is.True);
        }

        [Test]
        public async Task Commit_should_not_send_message_when_storage_tx_fails()
        {
            var dispatcher = new FakeDispatcher();
            var storageSession = new FakeSynchronizableStorageSession();
            storageSession.CompleteCallback = () => throw new Exception("session complete exception");

            using var session = new NonOutboxTransactionalSession(storageSession, new FakeMessageSession(), dispatcher, Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new FakeOpenSessionOptions());
            await session.Send(new object());
            Assert.ThrowsAsync<Exception>(async () => await session.Commit());

            Assert.That(dispatcher.Dispatched, Is.Empty, "should not have dispatched message");
        }

        [Test]
        public async Task Dispose_should_dispose_synchronized_storage_session()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            var session = new NonOutboxTransactionalSession(synchronizedStorageSession, new FakeMessageSession(), new FakeDispatcher(), Enumerable.Empty<IOpenSessionOptionsCustomization>());
            var options = new FakeOpenSessionOptions();
            await session.Open(options);

            session.Dispose();

            Assert.That(synchronizedStorageSession.Disposed, Is.True);
        }
    }
}