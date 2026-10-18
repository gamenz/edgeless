using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Edgeless.Core.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Edgeless.Interop;

/// <summary>Display-monitor queries (read-only).</summary>
internal static unsafe class MonitorApi
{
    public static MonitorDescriptor FromWindow(nint hwnd)
        => Describe(PInvoke.MonitorFromWindow(new HWND(hwnd), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST));

    public static IReadOnlyList<MonitorDescriptor> EnumerateAll()
    {
        var monitors = new List<MonitorDescriptor>();
        var handle = GCHandle.Alloc(monitors);
        try
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, &OnMonitor, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return monitors;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL OnMonitor(HMONITOR monitor, HDC hdc, RECT* clip, LPARAM state)
    {
        var monitors = (List<MonitorDescriptor>)GCHandle.FromIntPtr(state).Target!;
        monitors.Add(Describe(monitor));
        return true;
    }

    private static MonitorDescriptor Describe(HMONITOR monitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (!PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            return new MonitorDescriptor("", default);
        }

        var r = info.monitorInfo.rcMonitor;
        return new MonitorDescriptor(info.szDevice.ToString(), new RectI(r.left, r.top, r.right, r.bottom));
    }
}
