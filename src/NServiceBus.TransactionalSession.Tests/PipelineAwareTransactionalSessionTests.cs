namespace NServiceBus.TransactionalSession.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Fakes;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class PipelineAwareTransactionalSessionTests
    {
        [Test]
        public async Task Should_use_message_id_for_session_id()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext { MessageId = Guid.NewGuid().ToString() };
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            Assert.AreEqual(testableHandlerContext.MessageId, session.SessionId);
        }

        [Test]
        public async Task Open_should_throw_if_session_already_open()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());

            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Open(new PipelineAwareSessionOptions(testableHandlerContext)));

            StringAssert.Contains($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.", exception.Message);
        }

        [Test]
        public async Task Send_should_send_via_context()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            await session.Send(new object());

            Assert.That(testableHandlerContext.SentMessages, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task Publish_should_publish_via_context()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            await session.Publish(new object());

            Assert.That(testableHandlerContext.PublishedMessages, Has.Length.EqualTo(1));
        }

        [Test]
        public void Send_should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
        }

        [Test]
        public void Publish_should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
        }

        [Test]
        public async Task Should_expose_synchronized_storage_of_context()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            var testableHandlerContext = new TestableInvokeHandlerContext { SynchronizedStorageSession = synchronizedStorageSession };
            var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            Assert.AreSame(synchronizedStorageSession, session.SynchronizedStorageSession);
        }

        [Test]
        public void Should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.Throws<InvalidOperationException>(() => _ = session.SynchronizedStorageSession);

            StringAssert.Contains("The session has to be opened before accessing the SynchronizedStorageSession.", exception.Message);
        }

        [Test]
        public void Commit_should_throw_if_session_is_not_open()
        {
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Commit());

            StringAssert.Contains($"This session has not been opened yet.", exception.Message);
        }

        [Test]
        public async Task Commit_should_throw_when_already_committed()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));
            await session.Commit();

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Commit());

            StringAssert.Contains($"This session has already been committed. Complete all session operations before calling `Commit` or use a new session.", exception.Message);
        }

        [Test]
        public async Task Dispose_should_not_dispose_synchronized_storage_session()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            var testableHandlerContext = new TestableInvokeHandlerContext { SynchronizedStorageSession = synchronizedStorageSession };
            var session = new PipelineAwareTransactionalSession(Enumerable.Empty<IOpenSessionOptionsCustomization>());
            await session.Open(new PipelineAwareSessionOptions(testableHandlerContext));

            session.Dispose();

            Assert.IsFalse(synchronizedStorageSession.Disposed);
        }
    }
}