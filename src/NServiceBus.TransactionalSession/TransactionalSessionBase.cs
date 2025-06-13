namespace NServiceBus.TransactionalSession;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Extensibility;
using Persistence;
using Transport;

abstract class TransactionalSessionBase(ICompletableSynchronizedStorageSession synchronizedStorageSession,
    IMessageSession messageSession,
    IMessageDispatcher dispatcher,
    IEnumerable<IOpenSessionOptionsCustomization> customizations)
    : ITransactionalSession
{
    public ISynchronizedStorageSession SynchronizedStorageSession
    {
        get
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException(
                    "The session has to be opened before accessing the SynchronizedStorageSession.");
            }

            return synchronizedStorageSession;
        }
    }

    public string SessionId
    {
        get
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("The session has to be opened before accessing the SessionId.");
            }

            return openSessionOptions.SessionId;
        }
    }

    protected ContextBag Context
    {
        get
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("The session has to be opened before accessing the Context.");
            }

            return openSessionOptions.Extensions;
        }
    }

    protected OpenSessionOptions Options
    {
        get
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("The session has to be opened before accessing the Options.");
            }

            return openSessionOptions;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Options));

            openSessionOptions = value;
        }
    }

    [MemberNotNullWhen(true, nameof(openSessionOptions))]
    protected bool IsOpen => openSessionOptions is not null;

    public async Task Commit(CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidState();

        await CommitInternal(cancellationToken).ConfigureAwait(false);

        committed = true;
    }

    [MemberNotNull(nameof(Options))]
    public abstract Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default);

    protected abstract Task CommitInternal(CancellationToken cancellationToken = default);

    public async Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidState();

        sendOptions.GetExtensions().Set(pendingOperations);
        await messageSession.Send(message, sendOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidState();

        sendOptions.GetExtensions().Set(pendingOperations);
        await messageSession.Send(messageConstructor, sendOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidState();

        publishOptions.GetExtensions().Set(pendingOperations);
        await messageSession.Publish(message, publishOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default)
    {
        ThrowIfInvalidState();

        publishOptions.GetExtensions().Set(pendingOperations);
        await messageSession.Publish(messageConstructor, publishOptions, cancellationToken).ConfigureAwait(false);
    }

    protected void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(Dispose));
        }
    }

    protected void ThrowIfCommitted()
    {
        if (committed)
        {
            throw new InvalidOperationException("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.");
        }
    }

    void ThrowIfNotOpened()
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("This session has not been opened yet.");
        }
    }

    void ThrowIfInvalidState()
    {
        ThrowIfDisposed();
        ThrowIfCommitted();
        ThrowIfNotOpened();
    }

    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
    }

    protected readonly ICompletableSynchronizedStorageSession synchronizedStorageSession = synchronizedStorageSession;
    protected readonly IMessageDispatcher dispatcher = dispatcher;
    protected readonly IEnumerable<IOpenSessionOptionsCustomization> customizations = customizations;
    protected readonly PendingTransportOperations pendingOperations = new();
    protected bool disposed;
    OpenSessionOptions? openSessionOptions;
    bool committed;
}