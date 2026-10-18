using System.Runtime.InteropServices;

namespace Edgeless.TestWindow;

/// <summary>
/// Usage: Edgeless.TestWindow.exe --title T [--mode normal|reset-once|fight] [--delay-ms N] [--maximized] [--visible]
/// The window is fully transparent unless --visible is given (automated tests flash many windows).
/// reset-once: after the first external style change, restore the framed 800x600 window once after delay-ms.
/// fight: do that after every external style change.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new GameForm(Options.Parse(args)));
    }
}

internal sealed record Options(string Title, string Mode, int DelayMs, bool Maximized, bool Visible)
{
    public static Options Parse(string[] args)
    {
        string Value(string name, string fallback)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        return new Options(Value("--title", "Edgeless Test Window"), Value("--mode", "normal"),
            int.Parse(Value("--delay-ms", "200"), System.Globalization.CultureInfo.InvariantCulture),
            args.Contains("--maximized"), args.Contains("--visible"));
    }
}

internal sealed class GameForm : Form
{
    private const int WM_STYLECHANGED = 0x007D;
    private const int GWL_STYLE = -16;
    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOZORDER = 0x0004;

    private readonly Options _options;
    private readonly System.Windows.Forms.Timer _resetTimer = new();
    private bool _selfChanging;
    private int _resets;

    public GameForm(Options options)
    {
        _options = options;
        Text = options.Title;
        BackColor = Color.Black;
        DoubleBuffered = true;

        // Layered window with alpha 0: still visible to Win32 (same styles, IsWindowVisible) but nothing is drawn,
        // so automated runs do not flash full-screen windows.
        Opacity = options.Visible ? 1.0 : 0.0;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(100, 100);
        Size = new Size(800, 600);
        if (options.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        _resetTimer.Interval = Math.Max(1, options.DelayMs);
        _resetTimer.Tick += (_, _) => ResetFrame();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_STYLECHANGED && (int)m.WParam == GWL_STYLE && !_selfChanging && IsHandleCreated)
        {
            var wantsReset = _options.Mode == "fight" || (_options.Mode == "reset-once" && _resets == 0);
            if (wantsReset && !_resetTimer.Enabled)
            {
                _resetTimer.Start();
            }
        }
    }

    private void ResetFrame()
    {
        _resetTimer.Stop();
        _resets++;
        _selfChanging = true;
        var style = (uint)GetWindowLongPtrW(Handle, GWL_STYLE) | WS_OVERLAPPEDWINDOW;
        SetWindowLongPtrW(Handle, GWL_STYLE, (nint)style);
        SetWindowPos(Handle, 0, 100, 100, 800, 600, SWP_FRAMECHANGED | SWP_NOZORDER);
        _selfChanging = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resetTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);
}
