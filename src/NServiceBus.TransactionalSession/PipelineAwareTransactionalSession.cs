#nullable enable

namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Persistence;
    using Pipeline;

    sealed class PipelineAwareTransactionalSession : ITransactionalSession
    {
        public PipelineAwareTransactionalSession(PipelineInformationHolder pipelineInformationHolder)
            => this.pipelineInformationHolder = pipelineInformationHolder;

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "The session has to be opened before accessing the SynchronizedStorageSession.");
                }

                return HandlerContext!.SynchronizedStorageSession;
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

                return HandlerContext!.MessageId;
            }
        }

        // In the invoke handler context phase the synchronized storage is ready to use
        bool IsOpen => HandlerContext is not null;

        IInvokeHandlerContext? HandlerContext => pipelineInformationHolder.HandlerContext;

        public Task Open(OpenSessionOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // TODO: Should this throw instead?
            return Task.CompletedTask;
        }

        public Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return HandlerContext!.Send(message, sendOptions);
        }

        public Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return HandlerContext!.Send(messageConstructor, sendOptions);
        }

        public Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return HandlerContext!.Publish(message, publishOptions);
        }

        public Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions,
            CancellationToken cancellationToken = default)
        {
            ThrowIfInvalidState();

            cancellationToken.ThrowIfCancellationRequested();
            // unfortunately there is no way to marry both tokens together
            return HandlerContext!.Publish(messageConstructor, publishOptions);
        }

        public Task Commit(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // TODO: Should this throw instead?
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

        PipelineInformationHolder pipelineInformationHolder;
        bool disposed;
    }
}