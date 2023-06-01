#nullable enable
namespace NServiceBus.TransactionalSession
{
    using Pipeline;

    sealed class PipelineInformationHolder
    {
        public bool WithinPipeline { get; set; }
        public IInvokeHandlerContext? HandlerContext { get; set; }
    }
}