using System.Runtime.InteropServices;

namespace Edgeless.IntegrationTests.Support;

/// <summary>Test-only user32 calls that simulate the user (never used by the app itself).</summary>
internal static partial class TestUser32
{
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWNOACTIVATE = 4; // restores a minimized window without stealing focus
    public const uint SWP_NOZORDER = 0x0004;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "FindWindowW")]
    public static partial nint FindWindow(string? className, string? title);

    /// <summary>Polls <paramref name="condition"/> until true or the timeout elapses.</summary>
    public static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return condition();
    }
}
