namespace Edgeless.Platform;

/// <summary>Portable layout: config.json and logs\ live next to Edgeless.exe.</summary>
internal static class AppPaths
{
    public static string ExePath { get; } = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Edgeless.exe");

    public static string ExeDirectory { get; } = Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory;

    /// <summary>Folder holding config.json (the exe's folder).</summary>
    public static string ConfigDirectory => ExeDirectory;

    /// <summary>&lt;exe folder&gt;\logs.</summary>
    public static string LogDirectory { get; } = Path.Combine(ExeDirectory, "logs");

    /// <summary>True when a file can be created (and removed) in <paramref name="directory"/>.</summary>
    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".edgeless-write-test-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    public static string NotWritableMessage(string directory)
        => "Edgeless needs to write config.json and the logs folder next to its exe, but this folder is not writable:\n\n"
            + directory
            + "\n\nMove Edgeless.exe to a writable folder (for example under your user folder) and run it again.";
}
