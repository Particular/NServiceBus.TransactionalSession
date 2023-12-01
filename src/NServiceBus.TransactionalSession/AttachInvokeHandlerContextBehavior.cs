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
            var invocationState = context.Extensions.Get<TrackInvocationStateBehavior.InvocationState>();
            var transactionalSession = context.Builder.GetRequiredService<ITransactionalSession>();
            if (invocationState.ShouldOpen)
            {
                await transactionalSession.Open(new PipelineAwareSessionOptions(context), context.CancellationToken).ConfigureAwait(false);
            }

            await next(context).ConfigureAwait(false);

            invocationState.Invoked();

            if (invocationState.ShouldCommit)
            {
                await transactionalSession.Commit(context.CancellationToken).ConfigureAwait(false);
            }
        }
    }
}