namespace EunSlip.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Core_Assembly_IsLoadable()
    {
        Assert.NotNull(typeof(SmokeTests).Assembly.FullName);
    }
}
