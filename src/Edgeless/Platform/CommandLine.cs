using Edgeless.Core.Config;

namespace Edgeless.Platform;

internal sealed record CommandLineOptions(bool Autostart, bool ElevatedRestart, AutostartMode? SetAutostart);

internal static class CommandLine
{
    /// <summary>--autostart, --elevated-restart, --set-autostart off|normal|elevated. Unknown arguments are ignored.</summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var autostart = false;
        var elevatedRestart = false;
        AutostartMode? setAutostart = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--autostart":
                    autostart = true;
                    break;
                case "--elevated-restart":
                    elevatedRestart = true;
                    break;
                case "--set-autostart" when i + 1 < args.Length:
                    setAutostart = ParseMode(args[++i]);
                    break;
            }
        }

        return new CommandLineOptions(autostart, elevatedRestart, setAutostart);
    }

    private static AutostartMode? ParseMode(string value) => value.ToLowerInvariant() switch
    {
        "off" => AutostartMode.Off,
        "normal" => AutostartMode.Normal,
        "elevated" => AutostartMode.Elevated,
        _ => null,
    };
}
