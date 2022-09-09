namespace NServiceBus.AcceptanceTesting
{
    using System.Threading.Tasks;
    using Extensibility;
    using Persistence;

    class CustomTestingSynchronizedStorage : ISynchronizedStorage
    {
        public Task<CompletableSynchronizedStorageSession> OpenSession(ContextBag contextBag) =>
            Task.FromResult<CompletableSynchronizedStorageSession>(new CustomTestingSynchronizedStorageSession());
    }
}