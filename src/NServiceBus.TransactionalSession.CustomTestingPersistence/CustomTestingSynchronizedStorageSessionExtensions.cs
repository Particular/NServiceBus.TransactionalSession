namespace NServiceBus
{
    using System;
    using AcceptanceTesting;
    using Persistence;

    public static class CustomTestingSynchronizedStorageSessionExtensions
    {
        public static void DelayCommit(this ISynchronizedStorageSession session, TimeSpan delay)
        {
            var customSession = (CustomTestingSynchronizedStorageSession)session;
            customSession.DelayCommit(delay);
        }
    }
}