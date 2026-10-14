namespace ScreenAnnotation.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreAssemblyLoads() => typeof(CoreMarker).Assembly.Should().NotBeNull();
}
