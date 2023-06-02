namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    sealed class AttachInvokeHandlerContextBehavior : IBehavior<IInvokeHandlerContext, IInvokeHandlerContext>
    {
        public async Task Invoke(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
        {
            var transactionalSession = context.Builder.GetRequiredService<ITransactionalSession>();
            await transactionalSession.Open(new PipelineAwareSessionOptions(context), context.CancellationToken).ConfigureAwait(false);

            await next(context).ConfigureAwait(false);

            await transactionalSession.Commit(context.CancellationToken).ConfigureAwait(false);
        }
    }
}