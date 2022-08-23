using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ExecuteOnlyForEnvironmentWithAttribute : Attribute, IApplyToContext
{
    readonly string environmentVariableName;

    public ExecuteOnlyForEnvironmentWithAttribute(string environmentVariableName) =>
        this.environmentVariableName = environmentVariableName;

    public void ApplyToContext(TestExecutionContext context)
    {
        var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName);

        if (string.IsNullOrWhiteSpace(environmentVariableValue))
        {
            Assert.Ignore($"Ignoring because environment variable not present or white space: {environmentVariableName}");
        }
    }
}