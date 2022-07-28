﻿namespace NServiceBus.TransactionalSession
{
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;
    using Transport;

    class TransactionalSession : TransactionalSessionBase
    {
        public TransactionalSession(
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher) : base(synchronizedStorageSession, messageSession, dispatcher)
        {
        }

        public override async Task Commit(CancellationToken cancellationToken = default)
        {
            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await dispatcher.Dispatch(new TransportOperations(pendingOperations.Operations), new TransportTransaction(), cancellationToken).ConfigureAwait(false);
        }

        public override async Task Open(OpenSessionOptions options = null, CancellationToken cancellationToken = default)
        {
            await base.Open(options, cancellationToken).ConfigureAwait(false);

            await synchronizedStorageSession.Open(null, transportTransaction, Context, cancellationToken).ConfigureAwait(false);
        }
    }
}