namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;
    using Persistence;
    using Pipeline;
    using Routing;
    using Transport;

    class TransactionalSessionDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        readonly IOutboxStorage outboxStorage;
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;
        TimeSpan MaxCommitDelay = TimeSpan.FromSeconds(30);
        TimeSpan CommitDelayIncrement = TimeSpan.FromSeconds(10);

        public TransactionalSessionDelayControlMessageBehavior(IOutboxStorage outboxStorage, IMessageDispatcher dispatcher, string physicalQueueAddress)
        {
            this.outboxStorage = outboxStorage;
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            var isCommitControlMessage = context.Message.Headers.ContainsKey(OutboxTransactionalSession.ControlMessageSentAtHeaderName);

            if (isCommitControlMessage == false)
            {
                await next().ConfigureAwait(false);
                return;
            }

            var commitStartedAtText = context.Message.Headers[OutboxTransactionalSession.ControlMessageSentAtHeaderName];

            var commitStartedAt = DateTimeOffsetHelper.ToDateTimeOffset(commitStartedAtText);

            var messageId = context.MessageId;

            var outboxRecord = await outboxStorage.Get(messageId, context.Extensions, CancellationToken.None).ConfigureAwait(false);
            var transactionCommitted = outboxRecord != null;
            var timeSinceCommitStart = DateTimeOffset.UtcNow.Subtract(commitStartedAt);

            if (transactionCommitted || timeSinceCommitStart > MaxCommitDelay)
            {
                await next().ConfigureAwait(false);
                return;
            }

            Log.Debug($"Delaying transaction commit control messages for messageId={messageId}");

            await dispatcher.Dispatch(new TransportOperations(
                    new Transport.TransportOperation(
                        new OutgoingMessage(messageId, context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), ReadOnlyMemory<byte>.Empty),
                        new UnicastAddressTag(physicalQueueAddress),
                        new DispatchProperties(new Dictionary<string, string>
                        {
                            {Headers.DeliverAt, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow.Add(CommitDelayIncrement))},
                        }),
                        DispatchConsistency.Isolated
                        )
                    ), new TransportTransaction(), context.CancellationToken)
                .ConfigureAwait(false);

            throw new ConsumeMessageException();
        }

        static ILog Log = LogManager.GetLogger<TransactionalSessionDelayControlMessageBehavior>();

    }

    class TransactionalSessionControlMessageExceptionBehavior : Behavior<ITransportReceiveContext>
    {
        public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (ConsumeMessageException)
            {
                //HINT: swallow the exception to acknowledge the incoming message and prevent outbox from commiting
            }
        }
    }

    class ConsumeMessageException : Exception
    {
    }

    /// <summary>
    /// Provides <see cref="ITransactionalSession" />.
    /// </summary>
    public class TransactionalSessionFeature : Feature
    {
        /// <summary>
        /// See <see cref="Feature.Setup" />.
        /// </summary>
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            var isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped<ITransactionalSession>(sp =>
            {
                var physicalLocalQueueAddress = sp.GetRequiredService<ITransportAddressResolver>()
                    .ToTransportAddress(localQueueAddress);

                if (isOutboxEnabled)
                {
                    return new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        physicalLocalQueueAddress
                        );
                }

                return new TransactionalSession(
                    sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                    sessionCaptureTask.CapturedSession,
                    sp.GetRequiredService<IMessageDispatcher>());
            });

            //TODO: we should pass NoOpOutboxStorage here if not running with Outbox
            //TODO: what happens when someone turns off the Outbox but control messages are still in the input queue?
            context.Pipeline.Register(sp => new TransactionalSessionDelayControlMessageBehavior(
                sp.GetRequiredService<IOutboxStorage>(),
                sp.GetRequiredService<IMessageDispatcher>(),
                sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress)
                ), "Transaction commit control message delay behavior");

            context.Pipeline.Register(new TransactionalSessionControlMessageExceptionBehavior(),
                "Transaction commit control message delay acknowledgement behavior");
        }

        class SessionCaptureTask : FeatureStartupTask
        {
            public IMessageSession CapturedSession { get; set; }

            protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = new CancellationToken())
            {
                CapturedSession = session;
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        }
    }
}