using Edgeless.App;
using Edgeless.Platform;
using Edgeless.Shell;

namespace Edgeless;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = CommandLine.Parse(args);

        // Helper invocation (usually elevated via UAC) that only changes the autostart mode, then exits.
        if (options.SetAutostart is { } mode)
        {
            return new Autostart().Apply(mode, AppPaths.ExePath) ? 0 : 1;
        }

        // An elevated restart waits for the old instance to restore its windows and release the mutex.
        using var instance = SingleInstance.TryAcquire(options.ElevatedRestart ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
        if (instance is null)
        {
            SingleInstance.SignalExisting();
            return 0;
        }

        // Portable app: config.json and logs\ must be writable next to the exe (e.g. not under Program Files).
        if (!AppPaths.IsDirectoryWritable(AppPaths.ExeDirectory))
        {
            ErrorDialog.Show(AppPaths.NotWritableMessage(AppPaths.ExeDirectory));
            return 2;
        }

        return AppHost.Run(options);
    }
}
