namespace NServiceBus.TransactionalSession.Tests.Fakes;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Extensibility;
using Outbox;
using Persistence;
using Transport;

class FakeSynchronizedStorage : ISynchronizedStorageAdapter, ISynchronizedStorage
{
    FakeSynchronizedStorageSession session;

    public FakeSynchronizedStorage()
    {
        session = new FakeSynchronizedStorageSession(() =>
        {
            Completed = true;
            CompleteCallback?.Invoke();
        });
    }

    public List<(OutboxTransaction, ContextBag)> OpenedOutboxTransactionSessions { get; } = new();
    public List<ContextBag> OpenedTransactionSessions { get; set; } = new();
    public Func<OutboxTransaction, ContextBag, CompletableSynchronizedStorageSession> TryOpenCallback { get; set; } = null;
    public Action CompleteCallback { get; set; } = null;
    public bool Completed { get; private set; }

    public SynchronizedStorageSession Session => session;

    public Task<CompletableSynchronizedStorageSession> TryAdapt(OutboxTransaction transaction, ContextBag context)
    {
        if (transaction == null)
        {
            return Task.FromResult<CompletableSynchronizedStorageSession>(null);
        }

        OpenedOutboxTransactionSessions.Add((transaction, context));
        return TryOpenCallback != null
            ? Task.FromResult(TryOpenCallback(transaction, context))
            : Task.FromResult<CompletableSynchronizedStorageSession>(session);
    }

    public Task<CompletableSynchronizedStorageSession> TryAdapt(TransportTransaction transportTransaction,
        ContextBag context) => Task.FromResult<CompletableSynchronizedStorageSession>(null);

    public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag)
    {
        OpenedTransactionSessions.Add(contextBag);
        return Task.FromResult<CompletableSynchronizedStorageSession>(session);
    }

    class FakeSynchronizedStorageSession : CompletableSynchronizedStorageSession
    {
        Action completeCallback;

        public FakeSynchronizedStorageSession(Action completeCallback)
        {
            this.completeCallback = completeCallback;
        }

        public Task CompleteAsync()
        {
            completeCallback();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            //NOOP
        }
    }
}