using Edgeless.App;
using Edgeless.Platform;

namespace Edgeless;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = CommandLine.Parse(args);

        // An elevated restart waits for the old instance to restore its windows and release the mutex.
        using var instance = SingleInstance.TryAcquire(options.ElevatedRestart ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
        if (instance is null)
        {
            SingleInstance.SignalExisting();
            return 0;
        }

        return AppHost.Run(options);
    }
}
