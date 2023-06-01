namespace NServiceBus.TransactionalSession.Tests
{
    using System;
    using System.Threading.Tasks;
    using Fakes;
    using NUnit.Framework;
    using Testing;

    [TestFixture]
    public class PipelineAwareTransactionalSessionTests
    {
        [Test]
        public void Should_use_message_id_for_session_id()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext { MessageId = Guid.NewGuid().ToString() };
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = testableHandlerContext });

            Assert.AreEqual(testableHandlerContext.MessageId, session.SessionId);
        }

        [Test]
        public async Task Send_should_send_via_context()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = testableHandlerContext });

            await session.Send(new object());

            Assert.That(testableHandlerContext.SentMessages, Has.Length.EqualTo(1));
        }

        [Test]
        public async Task Publish_should_publish_via_context()
        {
            var testableHandlerContext = new TestableInvokeHandlerContext();
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = testableHandlerContext });

            await session.Publish(new object());

            Assert.That(testableHandlerContext.PublishedMessages, Has.Length.EqualTo(1));
        }

        [Test]
        public void Send_should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = null });

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Send(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
        }

        [Test]
        public void Publish_should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = null });

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Publish(new object()));

            StringAssert.Contains("This session has not been opened yet.", exception.Message);
        }

        [Test]
        public void Should_expose_synchronized_storage_of_context()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            var testableHandlerContext = new TestableInvokeHandlerContext { SynchronizedStorageSession = synchronizedStorageSession };
            var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = testableHandlerContext });

            Assert.AreSame(synchronizedStorageSession, session.SynchronizedStorageSession);
        }

        [Test]
        public void Should_throw_exception_when_session_not_opened()
        {
            using var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = null });

            var exception = Assert.Throws<InvalidOperationException>(() => _ = session.SynchronizedStorageSession);

            StringAssert.Contains("The session has to be opened before accessing the SynchronizedStorageSession.", exception.Message);
        }

        [Test]
        public void Dispose_should_not_dispose_synchronized_storage_session()
        {
            var synchronizedStorageSession = new FakeSynchronizableStorageSession();

            var testableHandlerContext = new TestableInvokeHandlerContext { SynchronizedStorageSession = synchronizedStorageSession };
            var session = new PipelineAwareTransactionalSession(new PipelineInformationHolder { WithinPipeline = true, HandlerContext = testableHandlerContext });

            session.Dispose();

            Assert.IsFalse(synchronizedStorageSession.Disposed);
        }
    }
}