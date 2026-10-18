using Edgeless.Core.Rules;

namespace Edgeless.Core.Tests.Rules;

public sealed class ExeNameMatcherTests
{
    [Theory]
    [InlineData(@"C:\Games\EldenRing.EXE", "eldenring.exe")]
    [InlineData(" Game ", "game.exe")]
    [InlineData("d:/x/y/Foo.exe", "foo.exe")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_HandlesPathCaseAndMissingExtension(string input, string expected)
        => Assert.Equal(expected, ExeNameMatcher.Normalize(input));

}
