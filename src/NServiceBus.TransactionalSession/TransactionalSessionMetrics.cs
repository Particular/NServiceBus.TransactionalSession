namespace NServiceBus.TransactionalSession;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

sealed class TransactionalSessionMetrics : IDisposable
{
    public TransactionalSessionMetrics(IMeterFactory meterFactory, string endpointName)
    {
        this.endpointName = endpointName;

        meter = meterFactory.Create("NServiceBus.TransactionalSession", "0.2.0");

        commitDuration = meter.CreateHistogram<double>(
            "nservicebus.transactional_session.commit.duration",
            unit: "s",
            description: "Duration of transactional session commit operations.");

        dispatchDuration = meter.CreateHistogram<double>(
            "nservicebus.transactional_session.dispatch.duration",
            unit: "s",
            description: "Duration of transactional session dispatch operations.");

        totalAttempts = meter.CreateHistogram<long>(
            "nservicebus.transactional_session.control_message.attempts",
            unit: "{attempt}",
            description: "Number of attempts to process the control message.");

        transitTime = meter.CreateHistogram<double>(
            "nservicebus.transactional_session.control_message.transit_time",
            unit: "s",
            description: "Control message transit time.");
    }

    public void RecordCommitMetrics(bool success, long startTicks, bool usingOutbox)
    {
        if (!commitDuration.Enabled)
        {
            return;
        }

        TagList tags;
        tags.Add("nservicebus.endpoint", endpointName);
        tags.Add("nservicebus.transactional_session.commit.outcome", success ? "success" : "failure");
        tags.Add("nservicebus.transactional_session.mode", usingOutbox ? "outbox" : "non_outbox");

        var elapsed = Stopwatch.GetElapsedTime(startTicks);
        commitDuration.Record(elapsed.TotalSeconds, tags);
    }

    public void RecordDispatchMetrics(long startTicks)
    {
        if (!dispatchDuration.Enabled)
        {
            return;
        }

        TagList tags;
        tags.Add("nservicebus.endpoint", endpointName);

        var elapsed = Stopwatch.GetElapsedTime(startTicks);
        dispatchDuration.Record(elapsed.TotalSeconds, tags);
    }

    public void RecordTransitTime(DateTimeOffset? timeSent)
    {
        if (!transitTime.Enabled || timeSent is not { } timeSentValue)
        {
            return;
        }

        TagList tags;
        tags.Add("nservicebus.endpoint", endpointName);

        transitTime.Record((DateTimeOffset.UtcNow - timeSentValue).TotalSeconds, tags);
    }

    public void RecordControlMessageOutcome(int currentAttempt, bool success)
    {
        if (!totalAttempts.Enabled)
        {
            return;
        }

        TagList tags;
        tags.Add("nservicebus.endpoint", endpointName);
        tags.Add("nservicebus.transactional_session.control_message.outcome", success ? "success" : "failure");

        totalAttempts.Record(currentAttempt, tags);
    }

    public void Dispose() => meter.Dispose();

    readonly Histogram<double> commitDuration;
    readonly Histogram<double> dispatchDuration;
    readonly Histogram<long> totalAttempts;
    readonly Histogram<double> transitTime;
    readonly string endpointName;
    readonly Meter meter;
}
