namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Outbox;
using Persistence;
using Routing;
using Transport;
using TransportTransportOperation = Transport.TransportOperation;
using OutboxTransportOperation = Outbox.TransportOperation;

sealed class OutboxTransactionalSession(IOutboxStorage outboxStorage,
    ICompletableSynchronizedStorageSession synchronizedStorageSession,
    IMessageSession messageSession,
    IMessageDispatcher dispatcher,
    IEnumerable<IOpenSessionOptionsCustomization> customizations,
    string physicalQueueAddress,
    bool isSendOnly,
    TransactionalSessionMetrics metrics) : TransactionalSessionBase(synchronizedStorageSession, messageSession, dispatcher, customizations)
{
    protected override Task OpenInternal(CancellationToken cancellationToken = default)
    {
        // Unfortunately this is the only way to make it possible for Transaction.Current to float up to the caller
        // to make sure SQLP and NHibernate work with the transaction scope
        var outboxTransactionTask = outboxStorage.BeginTransaction(Context, cancellationToken);
        return Open(outboxTransactionTask, cancellationToken);
    }

    async Task Open(Task<IOutboxTransaction> beginTransactionTask, CancellationToken cancellationToken)
    {
        outboxTransaction = await beginTransactionTask.ConfigureAwait(false);

        if (!await synchronizedStorageSession!.TryOpen(outboxTransaction, Context, cancellationToken).ConfigureAwait(false))
        {
            throw new Exception("Outbox and synchronized storage persister are not compatible.");
        }
    }

    protected override async Task CommitInternal(CancellationToken cancellationToken = default)
    {
        var startTicks = Stopwatch.GetTimestamp();

        if (pendingOperations.HasOperations)
        {
            await DispatchControlMessage(cancellationToken).ConfigureAwait(false);
        }

        metrics.RecordDispatchMetrics(startTicks);

        await synchronizedStorageSession!.CompleteAsync(cancellationToken).ConfigureAwait(false);
        // Disposing the session after complete to be compliant with the core behavior
        // in case complete throws the synchronized storage session will get disposed by the dispose or the container
        // disposing multiple times is safe
        synchronizedStorageSession.Dispose();
        synchronizedStorageSession = null;

        try
        {
            // The outbox record is only stored when there are operations to store. When there are no operations
            // it doesn't make sense to store an empty outbox record and mark it as dispatched as long as we are not
            // exposing the possibility to set the session ID from the outside. When the session ID is random and
            // there are not outgoing messages there is no way to correlate the outbox record with the transactional
            // session call and therefore the cost storing (within tx) and setting as dispatched (outside tx)
            // the outbox record is not justified.
            if (pendingOperations.HasOperations)
            {
                var outboxMessage =
                    new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
                await outboxStorage.Store(outboxMessage, outboxTransaction, Context, cancellationToken)
                    .ConfigureAwait(false);
            }

            await outboxTransaction!.Commit(cancellationToken).ConfigureAwait(false);
            // Disposing the outbox transaction after commit to be compliant with the core behavior
            // in case complete throws the synchronized storage session will get disposed by the dispose
            outboxTransaction.Dispose();
            outboxTransaction = null;

            metrics.RecordCommitMetrics(true, startTicks, usingOutbox: true);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            metrics.RecordCommitMetrics(false, startTicks, usingOutbox: true);
            throw new Exception($"Failed to commit the transactional session. This might happen if the maximum commit duration is exceeded{(isSendOnly ? $" or if the transactional session has not been enabled on the configured processor endpoint - {physicalQueueAddress}" : "")}", e);
        }
    }

    async Task DispatchControlMessage(CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>()
        {
            { Headers.MessageId, SessionId },
            { Headers.ControlMessageHeader, bool.TrueString },
            { RemainingCommitDurationHeaderName, Options.MaximumCommitDuration.ToString() },
            { CommitDelayIncrementHeaderName, Options.CommitDelayIncrement.ToString() },
            { AttemptHeaderName, "1" },
            { TimeSentHeaderName, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow) }
        };
        if (Options.HasMetadata)
        {
            foreach (KeyValuePair<string, string> keyValuePair in Options.Metadata)
            {
                headers.Add(keyValuePair.Key, keyValuePair.Value);
            }
        }

        var message = new OutgoingMessage(SessionId, headers, ReadOnlyMemory<byte>.Empty);
        var operation = new TransportTransportOperation(
            message,
            new UnicastAddressTag(physicalQueueAddress),
            null,
            DispatchConsistency.Isolated // Avoids promoting to distributed tx by not combining transport and persistence when both share same technology
        );
        var outgoingMessages = new TransportOperations(operation);
        await dispatcher.Dispatch(outgoingMessages, new TransportTransaction(), cancellationToken).ConfigureAwait(false);
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
            outboxTransaction?.Dispose();
        }

        base.Dispose(disposing);
    }

    static OutboxTransportOperation[] ConvertToOutboxOperations(TransportTransportOperation[] operations)
    {
        var transportOperations = new OutboxTransportOperation[operations.Length];
        int index = 0;
        foreach (TransportTransportOperation operation in operations)
        {
            SerializeRoutingStrategy(operation.AddressTag, operation.Properties);

            transportOperations[index] = new OutboxTransportOperation(operation.Message.MessageId, operation.Properties, operation.Message.Body, operation.Message.Headers);
            index++;
        }

        return transportOperations;
    }

    static void SerializeRoutingStrategy(AddressTag addressTag, Dictionary<string, string> options)
    {
        switch (addressTag)
        {
            case MulticastAddressTag indirect:
                options["EventType"] = indirect.MessageType.AssemblyQualifiedName ?? throw new InvalidOperationException("Message type cannot be null when using multicast routing.");
                return;
            case UnicastAddressTag direct:
                options["Destination"] = direct.Destination;
                return;
            default:
                throw new Exception($"Unknown routing strategy {addressTag.GetType().FullName}");
        }
    }

    IOutboxTransaction? outboxTransaction;

    public const string RemainingCommitDurationHeaderName = "NServiceBus.TransactionalSession.RemainingCommitDuration";
    public const string CommitDelayIncrementHeaderName = "NServiceBus.TransactionalSession.CommitDelayIncrement";
    public const string AttemptHeaderName = "NServiceBus.TransactionalSession.CommitDelayAttempt";
    public const string TimeSentHeaderName = "NServiceBus.TransactionalSession.TimeSent";
}