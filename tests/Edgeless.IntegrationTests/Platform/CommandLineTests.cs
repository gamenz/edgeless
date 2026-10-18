using Edgeless.Core.Config;
using Edgeless.Platform;

namespace Edgeless.IntegrationTests.Platform;

public sealed class CommandLineTests
{
    [Fact]
    public void CommandLine_ParsesFlags()
    {
        Assert.Equal(new CommandLineOptions(true, false, null), CommandLine.Parse(["--autostart"]));
        Assert.Equal(new CommandLineOptions(false, true, null), CommandLine.Parse(["--elevated-restart"]));
        Assert.Equal(AutostartMode.Elevated, CommandLine.Parse(["--set-autostart", "elevated"]).SetAutostart);
        Assert.Equal(AutostartMode.Off, CommandLine.Parse(["--set-autostart", "OFF"]).SetAutostart);
    }

    [Fact]
    public void CommandLine_IgnoresUnknownAndIncomplete()
    {
        Assert.Equal(new CommandLineOptions(false, false, null), CommandLine.Parse(["--bogus"]));
        Assert.Equal(new CommandLineOptions(false, false, null), CommandLine.Parse(["--set-autostart"]));
        Assert.Equal(new CommandLineOptions(false, false, null), CommandLine.Parse(["--set-autostart", "maybe"]));
    }
}
