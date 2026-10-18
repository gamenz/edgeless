using System.ComponentModel;
using System.Diagnostics;

namespace Edgeless.Platform;

internal static class Elevation
{
    private const int ErrorCancelled = 1223;

    public static bool IsElevated => Environment.IsPrivilegedProcess;

    /// <summary>Starts this exe again with the "runas" verb. False when the user cancels UAC or it fails.</summary>
    public static bool TryRestartElevated(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(AppPaths.ExePath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return process is not null;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    /// <summary>Opens a folder or file with its default handler (Explorer, editor).</summary>
    public static void OpenWithShell(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
