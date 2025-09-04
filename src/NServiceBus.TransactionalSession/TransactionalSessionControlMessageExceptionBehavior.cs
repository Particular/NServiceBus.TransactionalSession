namespace NServiceBus.TransactionalSession;

using System;
using System.Threading.Tasks;
using Pipeline;

class TransactionalSessionControlMessageExceptionBehavior(TransactionalSessionMetrics metrics)
    : IBehavior<ITransportReceiveContext,
        ITransportReceiveContext>
{
    public async Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
    {
        try
        {
            if (context.Message.TryGetDispatchMessage(out var controlMessage))
            {
                context.Extensions.Set(controlMessage);
            }

            await next(context).ConfigureAwait(false);

            if (controlMessage is not null)
            {
                metrics.RecordControlMessageOutcome(controlMessage.Attempt, true);
            }
        }
        catch (ConsumeMessageException)
        {
            //HINT: swallow the exception to acknowledge the incoming message and prevent outbox from commiting
        }
    }
}