namespace NServiceBus.TransactionalSession
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Pipeline;

    sealed class PipelineIndicatorBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
    {
        public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
        {
            var pipelineIndicator = context.Builder.GetRequiredService<PipelineIndicator>();
            pipelineIndicator.WithinPipeline = true;
            return next(context);
        }
    }
}