namespace NServiceBus.TransactionalSession.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Fakes;
    using NUnit.Framework;
    using Testing;
    using Transport;

    [TestFixture]
    public class TransactionalSessionDelayControlMessageBehaviorTests
    {
        [Test]
        public async Task Should_continue_pipeline_when_message_not_transactional_session_control_message()
        {
            var dispatcher = new FakeDispatcher();
            var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address");

            bool continued = false;
            await behavior.Invoke(new TestableIncomingPhysicalMessageContext(), _ =>
            {
                continued = true;
                return Task.CompletedTask;
            });

            Assert.IsTrue(continued);
            Assert.IsEmpty(dispatcher.Dispatched);
        }

        [Test]
        public async Task Should_stop_pipeline_and_return_when_control_message_has_been_retried_beyond_remaining_commit_duration()
        {
            var dispatcher = new FakeDispatcher();
            var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address");

            var messageContext = new TestableIncomingPhysicalMessageContext();
            messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName] = TimeSpan.FromSeconds(-10).ToString("c");
            messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName] = TimeSpan.FromSeconds(5).ToString("c");

            bool continued = false;
            await behavior.Invoke(messageContext, _ =>
            {
                continued = true;
                return Task.CompletedTask;
            });

            Assert.IsFalse(continued, "should not continue pipeline");
            Assert.IsEmpty(dispatcher.Dispatched);
        }

        [Test]
        public void Should_dispatch_new_control_message_and_throw_ConsumeMessageException()
        {
            const string queueAddress = "queue address";

            var dispatcher = new FakeDispatcher();
            var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, queueAddress);

            var messageContext = new TestableIncomingPhysicalMessageContext();
            messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName] = TimeSpan.FromSeconds(30).ToString("c");
            messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName] = TimeSpan.FromSeconds(10).ToString("c");
            messageContext.Message.Headers["custom-header-key"] = "custom-header-value";

            bool continued = false;
            Assert.ThrowsAsync<ConsumeMessageException>(async () => await behavior.Invoke(messageContext, _ =>
            {
                continued = true;
                return Task.CompletedTask;
            }));

            Assert.IsFalse(continued, "should not continue pipeline");
            Assert.IsNotEmpty(dispatcher.Dispatched);

            var controlMessage = dispatcher.Dispatched.Single().outgoingMessages.UnicastTransportOperations.Single();
            Assert.AreEqual(queueAddress, controlMessage.Destination);
            Assert.AreEqual(DispatchConsistency.Isolated, controlMessage.RequiredDispatchConsistency);
            Assert.AreEqual(TimeSpan.FromSeconds(20), controlMessage.Properties.DelayDeliveryWith.Delay);
            Assert.AreEqual(messageContext.MessageId, controlMessage.Message.MessageId);
            Assert.AreEqual(0, controlMessage.Message.Body.Length);
            Assert.AreEqual("custom-header-value", controlMessage.Message.Headers["custom-header-key"]);

        }
    }
}