namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

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
}