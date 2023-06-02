#nullable enable

namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;
    using Pipeline;

    sealed class PipelineAwareTransactionalSession : ITransactionalSession
    {
        public PipelineAwareTransactionalSession(IEnumerable<IOpenSessionOptionsCustomization> customizations) => this.customizations = customizations;

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SynchronizedStorageSession.");
                }

                return pipelineContext!.SynchronizedStorageSession;
            }
        }

        public string SessionId
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SessionId.");
                }

                return pipelineContext!.MessageId;
            }
        }

        // In the invoke handler context phase the synchronized storage is ready to use
        bool IsOpen => pipelineContext is not null;

        public Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfCommitted();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. {nameof(ITransactionalSession)}.{nameof(ITransactionalSession.Open)} should only be called once.");
            }

            pipelineContext = ((PipelineAwareSessionOptions)options).PipelineContext;

            foreach (var customization in customizations)
            {
                customization.Apply(options);
            }

            return Task.CompletedTask;
        }

        public Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return pipelineContext!.Send(message, sendOptions);
        }

        public Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return pipelineContext!.Send(messageConstructor, sendOptions);
        }

        public Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return pipelineContext!.Publish(message, publishOptions);
        }

        public Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions,
            CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return pipelineContext!.Publish(messageConstructor, publishOptions);
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();

            committed = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        void ThrowIfInvalidState()
        {
            ThrowIfDisposed();
            ThrowIfNotOpened();
            ThrowIfCommitted();
        }

        void ThrowIfNotOpened()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("This session has not been opened yet.");
            }
        }

        void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PipelineAwareTransactionalSession));
            }
        }

        void ThrowIfCommitted()
        {
            if (committed)
            {
                throw new InvalidOperationException("This session has already been committed. Complete all session operations before calling `Commit` or use a new session.");
            }
        }

        IInvokeHandlerContext? pipelineContext;
        readonly IEnumerable<IOpenSessionOptionsCustomization> customizations;
        bool disposed;
        bool committed;
    }
}