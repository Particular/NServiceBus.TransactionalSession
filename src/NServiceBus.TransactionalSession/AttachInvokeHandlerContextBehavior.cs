namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    sealed class AttachInvokeHandlerContextBehavior : IBehavior<IInvokeHandlerContext, IInvokeHandlerContext>
    {
        public Task Invoke(IInvokeHandlerContext context, Func<IInvokeHandlerContext, Task> next)
        {
            var pipelineIndicator = context.Builder.GetRequiredService<PipelineInformationHolder>();
            pipelineIndicator.HandlerContext = context;
            return next(context);
        }
    }
}