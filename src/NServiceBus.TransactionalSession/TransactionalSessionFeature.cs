namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Outbox;
    using Persistence;
    using Pipeline;
    using Routing;
    using Transport;

    class UnitOfWorkDelayControlMessageBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        readonly IOutboxStorage outboxStorage;
        readonly IMessageDispatcher dispatcher;
        readonly string physicalQueueAddress;
        TimeSpan MaxCommitDelay = TimeSpan.FromSeconds(29.5);

        public UnitOfWorkDelayControlMessageBehavior(IOutboxStorage outboxStorage, IMessageDispatcher dispatcher, string physicalQueueAddress)
        {
            this.outboxStorage = outboxStorage;
            this.dispatcher = dispatcher;
            this.physicalQueueAddress = physicalQueueAddress;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            if (context.Message.Headers.ContainsKey(OutboxTransactionalSession.ControlMessageSentAtHeaderName))
            {
                var headerValue = context.Message.Headers[OutboxTransactionalSession.ControlMessageSentAtHeaderName];

                var commitStartedAt = DateTimeOffsetHelper.ToDateTimeOffset(headerValue);

                var messageId = context.MessageId;

                var outboxRecord = await outboxStorage.Get(messageId, context.Extensions, CancellationToken.None).ConfigureAwait(false);
                var transactionNotCommitted = outboxRecord == null;

                if (DateTimeOffset.UtcNow.Subtract(commitStartedAt) < MaxCommitDelay && transactionNotCommitted)
                {
                    await dispatcher.Dispatch(new TransportOperations(
                            new Transport.TransportOperation(
                                new OutgoingMessage(messageId, context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), ReadOnlyMemory<byte>.Empty),
                                new UnicastAddressTag(physicalQueueAddress),
                                new DispatchProperties(new Dictionary<string, string>
                                {
                                    {Headers.DeliverAt, DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow.AddSeconds(5))},
                                }),
                                DispatchConsistency.Isolated
                                )
                            ), new TransportTransaction(), context.CancellationToken)
                        .ConfigureAwait(false);

                    throw new ConsumeMessageException();
                }
                else
                {
                    //TODO: let the transaction commit ot tombstone the record
                }
            }
        }

    }

    class UnitOfWorkControlMessageExceptionBehavior : Behavior<ITransportReceiveContext>
    {
        public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (ConsumeMessageException)
            {
                //TODO: swollow the transaction and commit
                throw;
            }
        }
    }

    class ConsumeMessageException : Exception
    {
    }

    public class TransactionalSessionFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            QueueAddress localQueueAddress = context.LocalQueueAddress();

            bool isOutboxEnabled = context.Settings.IsFeatureActive(typeof(Outbox));
            var sessionCaptureTask = new SessionCaptureTask();
            context.RegisterStartupTask(sessionCaptureTask);
            context.Services.AddScoped<ITransactionalSession>(sp =>
            {
                if (isOutboxEnabled)
                {
                    return new OutboxTransactionalSession(
                        sp.GetRequiredService<IOutboxStorage>(),
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>(),
                        sp.GetRequiredService<ITransportAddressResolver>().ToTransportAddress(localQueueAddress));
                }
                else
                {
                    return new TransactionalSession(
                        sp.GetRequiredService<ICompletableSynchronizedStorageSession>(),
                        sessionCaptureTask.CapturedSession,
                        sp.GetRequiredService<IMessageDispatcher>());
                }
            });
        }

        public class SessionCaptureTask : FeatureStartupTask
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