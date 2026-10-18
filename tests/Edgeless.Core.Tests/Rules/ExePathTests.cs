using Edgeless.Core.Rules;

namespace Edgeless.Core.Tests.Rules;

public sealed class ExePathTests
{
    [Fact]
    public void Normalize_TrimsAndFixesSeparators()
        => Assert.Equal(@"D:\Games\A\game.exe", ExePath.Normalize("  D:/Games/A/game.exe "));

    [Fact]
    public void Normalize_ResolvesDotSegments()
        => Assert.Equal(@"D:\Games\B\game.exe", ExePath.Normalize(@"D:\Games\A\..\B\game.exe"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("game.exe")]
    [InlineData(@"Games\game.exe")]
    [InlineData(@"\Games\game.exe")]
    public void Normalize_RejectsBlankAndRelative(string path)
        => Assert.Equal("", ExePath.Normalize(path));

    [Fact]
    public void AreSame_IgnoresCaseAndSeparators()
    {
        Assert.True(ExePath.AreSame(@"D:\Games\A\Game.EXE", "d:/games/a/game.exe"));
        Assert.False(ExePath.AreSame(@"D:\Games\A\game.exe", @"D:\Games\B\game.exe"));
        Assert.False(ExePath.AreSame("", ""));
        Assert.False(ExePath.AreSame("game.exe", "game.exe"));
    }

    [Fact]
    public void FileName_IsLowerCaseName()
    {
        Assert.Equal("eldenring.exe", ExePath.FileName(@"D:\Games\Elden Ring\EldenRing.EXE"));
        Assert.Equal("", ExePath.FileName(""));
    }
}
