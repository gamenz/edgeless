using Edgeless.Core.Rules;

namespace Edgeless.Core.Tests.Rules;

public sealed class ExclusionRulesTests
{
    [Theory]
    [InlineData("explorer.exe", "Shell_TrayWnd", false, true)]
    [InlineData("explorer.exe", "Shell_SecondaryTrayWnd", false, true)]
    [InlineData("explorer.exe", "Progman", false, true)]
    [InlineData("explorer.exe", "WorkerW", false, true)]
    [InlineData("explorer.exe", "CabinetWClass", false, false)]
    [InlineData("applicationframehost.exe", "ApplicationFrameWindow", false, true)]
    [InlineData("game.exe", "#32769", false, true)]
    [InlineData("game.exe", "UnityWndClass", true, true)]
    [InlineData("game.exe", "UnityWndClass", false, false)]
    public void IsExcluded_ShellWindows(string exe, string className, bool isOwnProcess, bool expected)
        => Assert.Equal(expected, ExclusionRules.IsExcluded(exe, className, isOwnProcess));
}
