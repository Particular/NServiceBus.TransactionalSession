namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Outbox;
    using Persistence;
    using Routing;
    using Transport;
    using TransportTransportOperation = Transport.TransportOperation;
    using OutboxTransportOperation = Outbox.TransportOperation;

    class OutboxTransactionalSession : TransactionalSessionBase, ITransactionalSession
    {
        public OutboxTransactionalSession(
            IOutboxStorage outboxStorage,
            ICompletableSynchronizedStorageSession synchronizedStorageSession,
            IMessageSession messageSession,
            IMessageDispatcher dispatcher,
            string physicalQueueAddress) : base(messageSession, dispatcher)
        {
            this.outboxStorage = outboxStorage;
            this.synchronizedStorageSession = synchronizedStorageSession;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public ISynchronizedStorageSession SynchronizedStorageSession
        {
            get
            {
                if (!IsOpen)
                {
                    throw new InvalidOperationException(
                        "Before accessing the SynchronizedStorageSession, make sure to open the session by calling the `Open`-method.");
                }

                return synchronizedStorageSession;
            }
        }

        public string SessionId { get; set; }

        ContextBag ITransactionalSession.PersisterSpecificOptions { get; } = new ContextBag();

        protected ContextBag Context => options.Extensions;

        protected bool IsOpen => options != null;

        public override async Task Commit(CancellationToken cancellationToken = default)
        {
            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, SessionId },
                { Headers.ControlMessageHeader, bool.TrueString },
                { RemainingCommitDurationHeaderName, options.MaximumCommitDuration.ToString() },
                { CommitDelayIncrementHeaderName, options.CommitDelayIncrement.ToString() },
            };
            if (options.HasMetadata)
            {
                foreach (KeyValuePair<string, string> keyValuePair in options.Metadata)
                {
                    headers.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            var message = new OutgoingMessage(SessionId, headers, ReadOnlyMemory<byte>.Empty);

            var outgoingMessages = new TransportOperations(new TransportTransportOperation(message, new UnicastAddressTag(physicalQueueAddress)));
            await dispatcher.Dispatch(outgoingMessages, new TransportTransaction(), cancellationToken).ConfigureAwait(false);

            var outboxMessage =
                new OutboxMessage(SessionId, ConvertToOutboxOperations(pendingOperations.Operations));
            await outboxStorage.Store(outboxMessage, outboxTransaction, Context, cancellationToken)
                .ConfigureAwait(false);

            await synchronizedStorageSession.CompleteAsync(cancellationToken).ConfigureAwait(false);

            await outboxTransaction.Commit(cancellationToken).ConfigureAwait(false);
        }

        public override Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            return base.Send(message, sendOptions, cancellationToken);
        }

        public override Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before sending any messages, make sure to open the session by calling the `Open`-method.");
            }

            return base.Send(messageConstructor, sendOptions, cancellationToken);
        }

        public override Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            return base.Publish(message, publishOptions, cancellationToken);
        }

        public override Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions,
            CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Before publishing any messages, make sure to open the session by calling the `Open`-method.");
            }

            return base.Publish(messageConstructor, publishOptions, cancellationToken);
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
            if (addressTag is MulticastAddressTag indirect)
            {
                options["EventType"] = indirect.MessageType.AssemblyQualifiedName;
                return;
            }

            if (addressTag is UnicastAddressTag direct)
            {
                options["Destination"] = direct.Destination;
                return;
            }

            throw new Exception($"Unknown routing strategy {addressTag.GetType().FullName}");
        }

        async Task ITransactionalSession.Open(OpenSessionOptions options, CancellationToken cancellationToken)
        {
            this.options = options ??= new OpenSessionOptions();

            if (IsOpen)
            {
                throw new InvalidOperationException($"This session is already open. Open should only be called once.");
            }

            SessionId = options.SessionId;

            outboxTransaction = await outboxStorage.BeginTransaction(Context, cancellationToken).ConfigureAwait(false);

            if (!await synchronizedStorageSession.TryOpen(outboxTransaction, Context, cancellationToken).ConfigureAwait(false))
            {
                throw new Exception("Outbox and synchronized storage persister are not compatible.");
            }
        }

        IOutboxTransaction outboxTransaction;
        OpenSessionOptions options;

        readonly string physicalQueueAddress;
        readonly IOutboxStorage outboxStorage;
        readonly ICompletableSynchronizedStorageSession synchronizedStorageSession;
        public const string RemainingCommitDurationHeaderName = "NServiceBus.TransactionalSession.RemainingCommitDuration";
        public const string CommitDelayIncrementHeaderName = "NServiceBus.TransactionalSession.CommitDelayIncrement";
    }
}