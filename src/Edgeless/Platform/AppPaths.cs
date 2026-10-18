namespace Edgeless.Platform;

internal static class AppPaths
{
    /// <summary>%APPDATA%\Edgeless (config.json).</summary>
    public static string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Edgeless");

    /// <summary>%LOCALAPPDATA%\Edgeless\logs.</summary>
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Edgeless", "logs");

    public static string ExePath { get; } = Environment.ProcessPath ?? "Edgeless.exe";
}
