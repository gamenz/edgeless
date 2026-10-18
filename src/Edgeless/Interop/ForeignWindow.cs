using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Edgeless.Core.Geometry;
using Edgeless.Core.Rules;
using Edgeless.Core.Styles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Interop;

/// <summary>Opaque copy of WINDOWPLACEMENT, so callers outside Interop never touch generated types.</summary>
internal readonly record struct WindowPlacementSnapshot(
    uint Flags, uint ShowCmd, RectI Normal, int MinX, int MinY, int MaxX, int MaxY);

/// <summary>Whitelisted operations on foreign top-level windows (CLAUDE.md section 1.2).</summary>
internal static unsafe class ForeignWindow
{
    private const int MaxClassName = 256;

    public static WindowStyles GetStyles(nint hwnd)
        => new((uint)PInvoke.GetWindowLongPtr(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_STYLE),
            (uint)PInvoke.GetWindowLongPtr(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE));

    /// <summary>Writes GWL_STYLE then GWL_EXSTYLE. Returns the Win32 error (0 = success).</summary>
    public static int SetStyles(nint hwnd, WindowStyles styles)
    {
        var error = SetLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, styles.Style);
        return error != 0 ? error : SetLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, styles.ExStyle);
    }

    public static RectI GetWindowRect(nint hwnd)
    {
        PInvoke.GetWindowRect(new HWND(hwnd), out var r);
        return new RectI(r.left, r.top, r.right, r.bottom);
    }

    public static (int Width, int Height) GetClientSize(nint hwnd)
    {
        PInvoke.GetClientRect(new HWND(hwnd), out var r);
        return (r.right - r.left, r.bottom - r.top);
    }

    public static WindowPlacementSnapshot GetPlacement(nint hwnd)
    {
        var p = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
        PInvoke.GetWindowPlacement(new HWND(hwnd), ref p);
        var n = p.rcNormalPosition;
        return new WindowPlacementSnapshot((uint)p.flags, (uint)p.showCmd, new RectI(n.left, n.top, n.right, n.bottom),
            p.ptMinPosition.X, p.ptMinPosition.Y, p.ptMaxPosition.X, p.ptMaxPosition.Y);
    }

    public static bool SetPlacement(nint hwnd, in WindowPlacementSnapshot s)
    {
        var p = new WINDOWPLACEMENT
        {
            length = (uint)sizeof(WINDOWPLACEMENT),
            flags = (WINDOWPLACEMENT_FLAGS)s.Flags,
            showCmd = (SHOW_WINDOW_CMD)s.ShowCmd,
            ptMinPosition = new System.Drawing.Point(s.MinX, s.MinY),
            ptMaxPosition = new System.Drawing.Point(s.MaxX, s.MaxY),
            rcNormalPosition = new RECT(s.Normal.Left, s.Normal.Top, s.Normal.Right, s.Normal.Bottom),
        };
        return PInvoke.SetWindowPlacement(new HWND(hwnd), in p);
    }

    /// <summary>Moves/resizes and applies the frame change. Returns the Win32 error (0 = success).</summary>
    public static int SetPosition(nint hwnd, RectI rect)
    {
        Marshal.SetLastSystemError(0);
        var ok = PInvoke.SetWindowPos(new HWND(hwnd), HWND.Null, rect.Left, rect.Top, rect.Width, rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
        return ok ? 0 : Marshal.GetLastSystemError();
    }

    /// <summary>Recomputes the non-client frame without moving, sizing or activating the window.</summary>
    public static void RedrawFrame(nint hwnd)
        => PInvoke.SetWindowPos(new HWND(hwnd), HWND.Null, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    public static void Restore(nint hwnd) => PInvoke.ShowWindow(new HWND(hwnd), SHOW_WINDOW_CMD.SW_RESTORE);

    public static bool IsWindow(nint hwnd) => PInvoke.IsWindow(new HWND(hwnd));

    public static bool IsVisible(nint hwnd) => PInvoke.IsWindowVisible(new HWND(hwnd));

    public static bool IsMinimized(nint hwnd) => PInvoke.IsIconic(new HWND(hwnd));

    public static bool IsMaximized(nint hwnd) => PInvoke.IsZoomed(new HWND(hwnd));

    public static nint GetForeground() => PInvoke.GetForegroundWindow();

    public static uint GetProcessId(nint hwnd)
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(new HWND(hwnd), &pid);
        return pid;
    }

    public static string GetClassName(nint hwnd)
    {
        var buffer = stackalloc char[MaxClassName];
        var length = PInvoke.GetClassName(new HWND(hwnd), new PWSTR(buffer), MaxClassName);
        return new string(buffer, 0, Math.Max(0, length));
    }

    public static string GetTitle(nint hwnd)
    {
        var length = PInvoke.GetWindowTextLength(new HWND(hwnd));
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        fixed (char* ptr = buffer)
        {
            var copied = PInvoke.GetWindowText(new HWND(hwnd), new PWSTR(ptr), buffer.Length);
            return new string(ptr, 0, Math.Max(0, copied));
        }
    }

    public static WindowCandidate Describe(nint hwnd)
    {
        var h = new HWND(hwnd);
        var cloaked = 0;
        var cloakedOk = PInvoke.DwmGetWindowAttribute(h, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded;
        return new WindowCandidate(
            hwnd,
            IsRoot: PInvoke.GetAncestor(h, GET_ANCESTOR_FLAGS.GA_ROOT) == h,
            HasOwner: !PInvoke.GetWindow(h, GET_WINDOW_CMD.GW_OWNER).IsNull,
            IsVisible: PInvoke.IsWindowVisible(h),
            IsCloaked: cloakedOk && cloaked != 0,
            Styles: GetStyles(hwnd),
            Bounds: GetWindowRect(hwnd));
    }

    public static List<nint> EnumTopLevel()
    {
        var windows = new List<nint>();
        var handle = GCHandle.Alloc(windows);
        try
        {
            PInvoke.EnumWindows(&OnWindow, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return windows;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL OnWindow(HWND hwnd, LPARAM state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return true;
    }

    private static int SetLong(nint hwnd, WINDOW_LONG_PTR_INDEX index, uint value)
    {
        // A 0 return is ambiguous (previous value may be 0), so clear the error first and check it after.
        Marshal.SetLastSystemError(0);
        var previous = PInvoke.SetWindowLongPtr(new HWND(hwnd), index, (nint)(int)value);
        return previous == 0 ? Marshal.GetLastSystemError() : 0;
    }
}
