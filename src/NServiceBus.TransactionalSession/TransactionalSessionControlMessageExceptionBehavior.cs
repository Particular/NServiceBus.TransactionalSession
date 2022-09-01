namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class TransactionalSessionControlMessageExceptionBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        public async Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ConsumeMessageException)
            {
                //HINT: swallow the exception to acknowledge the incoming message and prevent outbox from commiting
            }
        }
    }
}