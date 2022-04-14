namespace NServiceBus.TransactionalSession
{
    public class DumpingGround
    {
        public IMessageSession Instance { get; set; }
        public string PhysicalQueueAddress { get; set; }
        public bool IsOutboxEnabled { get; set; }
    }
}