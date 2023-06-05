#nullable enable
namespace NServiceBus.TransactionalSession
{
    sealed class PipelineIndicator
    {
        public bool WithinPipeline { get; set; }
    }
}