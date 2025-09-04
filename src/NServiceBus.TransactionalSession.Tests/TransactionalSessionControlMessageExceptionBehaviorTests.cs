namespace NServiceBus.TransactionalSession.Tests;

using System;
using System.Threading.Tasks;
using Fakes;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NUnit.Framework;
using Testing;

[TestFixture]
public class TransactionalSessionControlMessageExceptionBehaviorTests
{
    [Test]
    public async Task Should_continue_pipeline()
    {
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        var context = new TestableTransportReceiveContext();
        bool continued = false;
        await behavior.Invoke(context, _ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(continued, Is.True);
            Assert.That(context.Extensions.TryGet<DispatchMessage>(out _), Is.False);
        });
    }

    [Test]
    public void Should_swallow_consume_message_exception()
    {
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        Assert.DoesNotThrowAsync(async () =>
            await behavior.Invoke(new TestableTransportReceiveContext(), _ => throw new ConsumeMessageException())
        );
    }

    [Test]
    public void Should_not_swallow_other_exception()
    {
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        Assert.ThrowsAsync<Exception>(async () =>
            await behavior.Invoke(new TestableTransportReceiveContext(), _ => throw new Exception())
        );
    }

    [Test]
    public async Task Should_set_dispatch_message_when_headers_present()
    {
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(new TestMeterFactory(), "endpointName"));

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var messageContext = new TestableTransportReceiveContext();
        messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName] = TimeSpan.FromSeconds(10).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName] = TimeSpan.FromSeconds(5).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.AttemptHeaderName] = "3";
        messageContext.Message.Headers[OutboxTransactionalSession.TimeSentHeaderName] = DateTimeOffsetHelper.ToWireFormattedString(now);

        bool continued = false;
        await behavior.Invoke(messageContext, _ =>
        {
            continued = true;
            return Task.CompletedTask;
        });

        Assert.Multiple(() =>
        {
            Assert.That(continued, Is.True);
            Assert.That(messageContext.Extensions.TryGet<DispatchMessage>(out var dispatchMessage), Is.True);
            Assert.That(dispatchMessage.RemainingCommitDuration, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(dispatchMessage.CommitDelayIncrement, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(dispatchMessage.Attempt, Is.EqualTo(3));
            Assert.That(dispatchMessage.TimeSent, Is.EqualTo(now).Within(TimeSpan.FromMilliseconds(100)));
        });
    }

    [Test]
    public async Task Should_emit_metrics_on_control_message_with_recognized_attempt()
    {
        var meterFactory = new TestMeterFactory();
        var totalAttempts = new MetricCollector<long>(meterFactory, "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.control_message.attempts");
        string endpointName = "endpointName";
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(meterFactory, endpointName));

        var messageContext = new TestableTransportReceiveContext();
        messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName] = TimeSpan.FromSeconds(-10).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName] = TimeSpan.FromSeconds(5).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.AttemptHeaderName] = "5";

        await behavior.Invoke(messageContext, _ => Task.CompletedTask);

        var totalAttemptsSnapshot = totalAttempts.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(totalAttemptsSnapshot.Count, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Value, Is.EqualTo(5));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.transactional_session.control_message.outcome"], Is.EqualTo("success"));
        });
    }

    [Test]
    public async Task Should_emit_metrics_on_control_message_with_unrecognized_attempt()
    {
        var meterFactory = new TestMeterFactory();
        var totalAttempts = new MetricCollector<long>(meterFactory, "NServiceBus.TransactionalSession",
            "nservicebus.transactional_session.control_message.attempts");
        string endpointName = "endpointName";
        var behavior = new TransactionalSessionControlMessageExceptionBehavior(new TransactionalSessionMetrics(meterFactory, endpointName));

        var messageContext = new TestableTransportReceiveContext();
        messageContext.Message.Headers[OutboxTransactionalSession.RemainingCommitDurationHeaderName] = TimeSpan.FromSeconds(-10).ToString("c");
        messageContext.Message.Headers[OutboxTransactionalSession.CommitDelayIncrementHeaderName] = TimeSpan.FromSeconds(5).ToString("c");

        await behavior.Invoke(messageContext, _ => Task.CompletedTask);

        var totalAttemptsSnapshot = totalAttempts.GetMeasurementSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(totalAttemptsSnapshot.Count, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Value, Is.EqualTo(1));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.endpoint"], Is.EqualTo(endpointName));
            Assert.That(totalAttemptsSnapshot[0].Tags["nservicebus.transactional_session.control_message.outcome"], Is.EqualTo("success"));
        });
    }
}