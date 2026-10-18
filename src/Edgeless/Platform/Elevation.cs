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

    /// <summary>Runs this exe elevated with <paramref name="arguments"/> and waits. Exit code, or -1 if cancelled/failed.</summary>
    public static int RunElevatedAndWait(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(AppPaths.ExePath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
            {
                return -1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception)
        {
            return -1;
        }
    }

    /// <summary>Opens a folder or file with its default handler (Explorer, editor).</summary>
    public static void OpenWithShell(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
