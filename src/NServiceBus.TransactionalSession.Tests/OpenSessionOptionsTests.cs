namespace NServiceBus.TransactionalSession.Tests;

using System;
using Fakes;
using NUnit.Framework;

[TestFixture]
public class OpenSessionOptionsTests
{
    [Test]
    public void Should_throw_when_commit_delay_increment_is_negative()
    {
        var options = new FakeOpenSessionOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.CommitDelayIncrement = TimeSpan.FromSeconds(-1));
    }
}
