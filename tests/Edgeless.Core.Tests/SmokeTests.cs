using Edgeless.Core.Styles;

namespace Edgeless.Core.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Core_assembly_loads()
        => Assert.Equal("Edgeless.Core", typeof(StyleFlags).Assembly.GetName().Name);
}
