namespace NServiceBus.TransactionalSession
{
    using Pipeline;

    sealed class PipelineAwareSessionOptions : OpenSessionOptions
    {
        public PipelineAwareSessionOptions(IInvokeHandlerContext pipelineContext) => PipelineContext = pipelineContext;

        public IInvokeHandlerContext PipelineContext { get; }
    }
}