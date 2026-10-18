using System.Diagnostics;

namespace Edgeless.IntegrationTests.Support;

/// <summary>Starts Edgeless.TestWindow.exe and waits for its main window.</summary>
public sealed class TestWindowProcess : IDisposable
{
    /// <summary>Full path of the fake game exe; profiles match it by path.</summary>
    public static readonly string ExePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Edgeless.TestWindow.exe"));

    private readonly Process _process;

    private TestWindowProcess(Process process, nint hwnd)
    {
        _process = process;
        Hwnd = hwnd;
    }

    public nint Hwnd { get; }

    public uint Pid => (uint)_process.Id;

    public bool HasExited => _process.HasExited;

    public static TestWindowProcess Start(string mode = "normal", int delayMs = 200, bool maximized = false)
    {
        var exe = ExePath;
        var args = $"--title \"Edgeless Test {Guid.NewGuid():N}\" --mode {mode} --delay-ms {delayMs}"
            + (maximized ? " --maximized" : "");
        var process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false })
            ?? throw new InvalidOperationException("TestWindow did not start");

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0)
            {
                Thread.Sleep(200); // let the form finish its first layout
                return new TestWindowProcess(process, process.MainWindowHandle);
            }

            Thread.Sleep(50);
        }

        process.Kill();
        throw new TimeoutException("TestWindow main window did not appear");
    }

    public void Kill()
    {
        if (!_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit(5000);
        }
    }

    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }
}
