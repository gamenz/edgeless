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

    [Fact]
    public void Matches_ComparesFileNamesOnly()
    {
        Assert.True(ExeNameMatcher.Matches("eldenring", @"D:\x\eldenring.exe"));
        Assert.True(ExeNameMatcher.Matches("ELDENRING.exe", @"D:\x\EldenRing.exe"));
        Assert.False(ExeNameMatcher.Matches("ring.exe", @"D:\x\eldenring.exe"));
        Assert.False(ExeNameMatcher.Matches("", @"D:\x\eldenring.exe"));
    }
}
