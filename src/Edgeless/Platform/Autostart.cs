using System.Diagnostics;
using Edgeless.Core.Config;
using Microsoft.Win32;

namespace Edgeless.Platform;

/// <summary>
/// Start with Windows (spec section 4.10): HKCU Run value (normal) or a highest-privilege logon task (elevated).
/// The system state is the source of truth; config.json only mirrors it.
/// </summary>
internal sealed class Autostart(
    string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "Edgeless",
    string taskName = "Edgeless")
{
    public AutostartMode GetCurrent()
    {
        if (ReadRunValue() is not null)
        {
            return AutostartMode.Normal;
        }

        return TaskExists() ? AutostartMode.Elevated : AutostartMode.Off;
    }

    /// <summary>Switches to <paramref name="mode"/>, removing the other mechanism. Asks for UAC only when needed.</summary>
    public bool Apply(AutostartMode mode, string exePath)
    {
        var needsAdmin = mode == AutostartMode.Elevated || TaskExists();
        if (needsAdmin && !Elevation.IsElevated)
        {
            return Elevation.RunElevatedAndWait($"--set-autostart {mode.ToString().ToLowerInvariant()}") == 0;
        }

        switch (mode)
        {
            case AutostartMode.Normal:
                WriteRunValue(exePath);
                return DeleteTaskIfExists();
            case AutostartMode.Elevated:
                DeleteRunValue();
                return RunSchtasks(BuildCreateTaskArguments(taskName, exePath)) == 0;
            default:
                DeleteRunValue();
                return DeleteTaskIfExists();
        }
    }

    /// <summary>Points the current autostart entry at <paramref name="exePath"/> if the exe was moved.</summary>
    public void RepairPath(string exePath)
    {
        var current = GetCurrent();
        if (current == AutostartMode.Normal && ReadRunValue() != RunCommand(exePath))
        {
            WriteRunValue(exePath);
        }
        else if (current == AutostartMode.Elevated && Elevation.IsElevated)
        {
            RunSchtasks(BuildCreateTaskArguments(taskName, exePath)); // /F overwrites with the current path
        }
    }

    internal static string BuildCreateTaskArguments(string taskName, string exePath)
        => $"/Create /F /TN \"{taskName}\" /SC ONLOGON /RL HIGHEST /TR \"\\\"{exePath}\\\" --autostart\"";

    private static string RunCommand(string exePath) => $"\"{exePath}\" --autostart";

    private string? ReadRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
        return key?.GetValue(valueName) as string;
    }

    private void WriteRunValue(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath);
        key.SetValue(valueName, RunCommand(exePath), RegistryValueKind.String);
    }

    private void DeleteRunValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private bool TaskExists() => RunSchtasks($"/Query /TN \"{taskName}\"") == 0;

    private bool DeleteTaskIfExists() => !TaskExists() || RunSchtasks($"/Delete /F /TN \"{taskName}\"") == 0;

    private static int RunSchtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
            {
                return -1;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
