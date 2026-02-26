// filepath: /Users/nix/dev/NServiceBus.TransactionalSession/src/NServiceBus.TransactionalSession.Tests/OpenSessionOptionsTests.cs
namespace NServiceBus.TransactionalSession.Tests;

using System;
using NUnit.Framework;

public class OpenSessionOptionsTests
{
    [Test]
    [TestCase<int>(-1)]
    [TestCase<int>(0)]
    public void Should_throw_exception_when_commit_delay_increment_is_less_than_or_equal_to_zero(int commitDelayIncrement) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var options = new CustomTestingPersistenceOpenSessionOptions
            {
                CommitDelayIncrement = TimeSpan.FromSeconds(commitDelayIncrement),
                MaximumCommitDuration = TimeSpan.FromSeconds(8)
            };
        });
    class CustomTestingPersistenceOpenSessionOptions : OpenSessionOptions { }
}

