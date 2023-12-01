namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Unicast;

    sealed class TrackInvocationStateBehavior : IBehavior<IIncomingLogicalMessageContext, IIncomingLogicalMessageContext>
    {
        readonly MessageHandlerRegistry messageHandlerRegistry;

        public TrackInvocationStateBehavior(MessageHandlerRegistry messageHandlerRegistry) => this.messageHandlerRegistry = messageHandlerRegistry;

        public Task Invoke(IIncomingLogicalMessageContext context, Func<IIncomingLogicalMessageContext, Task> next)
        {
            var handlersToInvoke = messageHandlerRegistry.GetHandlersFor(context.Message.MessageType);
            context.Extensions.Set(new InvocationState(handlersToInvoke.Count));
            return next(context);
        }

        internal sealed class InvocationState(int numberOfHandlersCounter)
        {
            public bool ShouldOpen => NumberOfInvocations == NumberOfHandlersCounter;

            int NumberOfHandlersCounter { get; } = numberOfHandlersCounter;
            int NumberOfInvocations { get; set; } = numberOfHandlersCounter;

            public bool Invoked() => NumberOfInvocations-- == 0;
        }
    }
}