namespace NServiceBus.TransactionalSession;

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Persistence;
using Transport;

sealed class NonOutboxTransactionalSession(ICompletableSynchronizedStorageSession synchronizedStorageSession,
    IMessageSession messageSession,
    IMessageDispatcher dispatcher,
    IEnumerable<IOpenSessionOptionsCustomization> customizations,
    TransactionalSessionMetrics metrics)
    : TransactionalSessionBase(synchronizedStorageSession, messageSession, dispatcher, customizations)
{
    protected override async Task OpenInternal(CancellationToken cancellationToken = default)
        => await synchronizedStorageSession.Open(null, new TransportTransaction(), Context, cancellationToken).ConfigureAwait(false);

    protected override async Task CommitInternal(CancellationToken cancellationToken = default)
    {
        var startTicks = Stopwatch.GetTimestamp();

        await synchronizedStorageSession!.CompleteAsync(cancellationToken).ConfigureAwait(false);

        // Disposing the session after complete to be compliant with the core behavior
        // in case complete throws the synchronized storage session will get disposed by the dispose or the container
        // disposing multiple times is safe
        synchronizedStorageSession.Dispose();
        synchronizedStorageSession = null;

        metrics.RecordCommitMetrics(true, startTicks, usingOutbox: false);

        await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            synchronizedStorageSession?.Dispose();
        }

        base.Dispose(disposing);
    }

    ICompletableSynchronizedStorageSession? synchronizedStorageSession = synchronizedStorageSession;
}