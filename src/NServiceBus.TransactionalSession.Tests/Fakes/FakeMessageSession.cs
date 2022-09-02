namespace NServiceBus.TransactionalSession.Tests.Fakes
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Routing;
    using Testing;
    using Transport;

    class FakeMessageSession : TestableMessageSession
    {
        public override Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(sendOptions);
            return base.Send(message, sendOptions, cancellationToken);
        }

        public override Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(sendOptions);
            return base.Send(messageConstructor, sendOptions, cancellationToken);
        }

        public override Task Publish(object message, PublishOptions publishOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(publishOptions);
            return base.Publish(message, publishOptions, cancellationToken);
        }

        public override Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions,
            CancellationToken cancellationToken = new CancellationToken())
        {
            AddToPendingOperations(publishOptions);
            return base.Publish(messageConstructor, publishOptions, cancellationToken);
        }

        static void AddToPendingOperations(ExtendableOptions sendOptions)
        {
            // we need to fake the pipeline behavior to add outgoing messages to the PendingTransportOperations for the TransactionalSession to work.
            var pendingOperations = sendOptions.GetExtensions().Get<PendingTransportOperations>();
            pendingOperations.Add(new TransportOperation(
                new OutgoingMessage(sendOptions.GetMessageId() ?? Guid.NewGuid().ToString(),
                    sendOptions.GetHeaders().ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Encoding.UTF8.GetBytes("fake body")),
                new UnicastAddressTag("fake address")));
        }
    }
}