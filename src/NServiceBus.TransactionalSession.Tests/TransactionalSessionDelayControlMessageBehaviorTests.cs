namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Fakes;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
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
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address", new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

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
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address", new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Extensions.Set(new DispatchMessage(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(-10)));

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
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, queueAddress, new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Extensions.Set(new DispatchMessage(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)));
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
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.TimeSentHeaderName], Is.Not.Empty);
            Assert.That(controlMessage.Message.Headers[OutboxTransactionalSession.AttemptHeaderName], Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task Should_emit_metrics_when_message_beyond_remaining_commit_duration_and_attempts_unrecognized()
    {
        var dispatcher = new FakeDispatcher();
        var meterFactory = new TestMeterFactory();
        var totalAttempts = new MetricCollector<long>(meterFactory, "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.control_message.attempts");
        var endpointName = "endpointName";
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address", new TransactionalSessionMetrics(meterFactory, endpointName));

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Extensions.Set(new DispatchMessage(1, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(-10)));

        await behavior.Invoke(messageContext, _ => Task.CompletedTask);

        var totalAttemptsSnapshot = totalAttempts.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(totalAttemptsSnapshot.Count, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Value, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.transactional_session.control_message.outcome"], Is.EqualTo("failure"));
        });
    }

    [Test]
    public async Task Should_emit_metrics_when_message_beyond_remaining_commit_duration_and_attempts_recognized()
    {
        var dispatcher = new FakeDispatcher();
        var meterFactory = new TestMeterFactory();
        var totalAttempts = new MetricCollector<long>(meterFactory, "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.control_message.attempts");
        var endpointName = "endpointName";
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, "queue address", new TransactionalSessionMetrics(meterFactory, endpointName));

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Extensions.Set(new DispatchMessage(5, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(-10)));

        await behavior.Invoke(messageContext, _ => Task.CompletedTask);

        var totalAttemptsSnapshot = totalAttempts.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(totalAttemptsSnapshot.Count, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Value, Is.EqualTo(5));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.transactional_session.control_message.outcome"], Is.EqualTo("failure"));
        });
    }

    [Test]
    public void Should_record_transit_time_when_recognized_time_sent()
    {
        const string queueAddress = "queue address";

        var dispatcher = new FakeDispatcher();
        var meterFactory = new TestMeterFactory();
        var transitTimeHistogram = new MetricCollector<double>(meterFactory, "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.control_message.transit_time");
        var endpointName = "endpointName";
        var behavior = new TransactionalSessionDelayControlMessageBehavior(dispatcher, queueAddress, new TransactionalSessionMetrics(meterFactory, endpointName));

        var messageContext = new TestableIncomingPhysicalMessageContext();
        messageContext.Extensions.Set(new DispatchMessage(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), timeSent: DateTimeOffset.UtcNow));

        Assert.ThrowsAsync<ConsumeMessageException>(async () => await behavior.Invoke(messageContext, _ => Task.CompletedTask));

        var transitTimeSnapshot = transitTimeHistogram.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(transitTimeSnapshot.Count, Is.EqualTo(1));
            Assert.That(transitTimeSnapshot[0].Value, Is.Not.EqualTo(0));
            Assert.That(transitTimeSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
        });
    }
}