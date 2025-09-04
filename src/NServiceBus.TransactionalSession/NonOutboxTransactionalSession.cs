namespace NServiceBus.TransactionalSession;

using System;
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
    protected override async Task CommitInternal(CancellationToken cancellationToken = default)
    {
        var startTicks = Stopwatch.GetTimestamp();
        await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);
        metrics.RecordCommitMetrics(true, startTicks, usingOutbox: false);

        await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
    }

    public override async Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ThrowIfCommitted();

        if (IsOpen)
        {
            throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
        }

        Options = options;

        foreach (var customization in customizations)
        {
            customization.Apply(Options);
        }

        await synchronizedStorageSession.Open(null, new TransportTransaction(), Context, cancellationToken).ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            synchronizedStorageSession.Dispose();
        }

        base.Dispose(disposing);
    }
}