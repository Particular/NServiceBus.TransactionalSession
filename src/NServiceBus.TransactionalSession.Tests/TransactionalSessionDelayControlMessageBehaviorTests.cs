namespace NServiceBus.TransactionalSession.Tests;

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

        Assert.Multiple(() =>
        {
            Assert.That(continued, Is.True);
            Assert.That(dispatcher.Dispatched, Is.Empty);
        });
    }

    [Test]
    public async Task Should_stop_pipeline_and_return_when_control_message_has_been_retried_beyond_remaining_commit_duration()
    {
        var dispatcher = new FakeDispatcher();
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address");

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDuration] = TimeSpan.FromSeconds(-10).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeader] = TimeSpan.FromSeconds(5).ToString("c");

        bool continued = false;
        await behavior.Invoke(messageContext, _ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(continued, Is.False, "should not continue pipeline");
            Assert.That(dispatcher.Dispatched, Is.Empty);
        });
    }

    [Test]
    public void Should_dispatch_new_control_message_and_throw_ConsumeMessageException()
    {
        const string queueAddress = "queue address";

        var dispatcher = new FakeDispatcher();
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, queueAddress);

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDuration] = TimeSpan.FromSeconds(30).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeader] = TimeSpan.FromSeconds(10).ToString("c");
        messageContext.Message.Headers["custom-header-key"] = "custom-header-value";

        bool continued = false;
        Assert.ThrowsAsync<ConsumeMessageException>(async () => await behavior.Invoke(messageContext, _ =>
        {
            continued = true;
            return Task.CompletedTask;
        }));

        Assert.Multiple(() =>
        {
            Assert.That(continued, Is.False, "should not continue pipeline");
            Assert.That(dispatcher.Dispatched, Is.Not.Empty);
        });

        var controlMessage = dispatcher.Dispatched.Single().outgoingMessages.UnicastTransportOperations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(controlMessage.Destination, Is.EqualTo(queueAddress));
            Assert.That(controlMessage.RequiredDispatchConsistency, Is.EqualTo(DispatchConsistency.Isolated));
            Assert.That(controlMessage.Properties.DelayDeliveryWith.Delay, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(controlMessage.Message.MessageId, Is.EqualTo(messageContext.MessageId));
            Assert.That(controlMessage.Message.Body.Length, Is.EqualTo(0));
            Assert.That(controlMessage.Message.Headers["custom-header-key"], Is.EqualTo("custom-header-value"));
        });

    }
}