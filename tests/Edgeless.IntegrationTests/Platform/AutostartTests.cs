using Edgeless.Core.Config;
using Edgeless.Platform;
using Microsoft.Win32;

namespace Edgeless.IntegrationTests.Platform;

/// <summary>Uses a private test key instead of the real Run key; the scheduled task is checked manually.</summary>
public sealed class AutostartTests : IDisposable
{
    private readonly string _keyPath = @"Software\Edgeless.Tests\" + Guid.NewGuid().ToString("N") + @"\Run";
    private readonly Autostart _autostart;

    public AutostartTests() => _autostart = new Autostart(_keyPath, "Edgeless", "Edgeless.Tests.NoSuchTask");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\Edgeless.Tests", throwOnMissingSubKey: false);

    private string? RunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue("Edgeless") as string;
    }

    [Fact]
    public void Normal_WritesQuotedPathWithFlag()
    {
        Assert.True(_autostart.Apply(AutostartMode.Normal, @"C:\My Games\Edgeless.exe"));

        Assert.Equal("\"C:\\My Games\\Edgeless.exe\" --autostart", RunValue());
        Assert.Equal(AutostartMode.Normal, _autostart.GetCurrent());
    }

    [Fact]
    public void Off_RemovesValue()
    {
        _autostart.Apply(AutostartMode.Normal, @"C:\Edgeless.exe");

        Assert.True(_autostart.Apply(AutostartMode.Off, @"C:\Edgeless.exe"));

        Assert.Null(RunValue());
        Assert.Equal(AutostartMode.Off, _autostart.GetCurrent());
    }

    [Fact]
    public void RepairPath_UpdatesMovedExe()
    {
        _autostart.Apply(AutostartMode.Normal, @"C:\Old\Edgeless.exe");

        _autostart.RepairPath(@"D:\New\Edgeless.exe");

        Assert.Equal("\"D:\\New\\Edgeless.exe\" --autostart", RunValue());
    }

    [Fact]
    public void RepairPath_WhenOff_WritesNothing()
    {
        _autostart.RepairPath(@"D:\New\Edgeless.exe");

        Assert.Null(RunValue());
    }

    [Fact]
    public void BuildTaskArguments_QuotesExeAndRunsAtLogonWithHighestPrivileges()
        => Assert.Equal(
            "/Create /F /TN \"Edgeless\" /SC ONLOGON /RL HIGHEST /TR \"\\\"C:\\My Games\\Edgeless.exe\\\" --autostart\"",
            Autostart.BuildCreateTaskArguments("Edgeless", @"C:\My Games\Edgeless.exe"));
}
