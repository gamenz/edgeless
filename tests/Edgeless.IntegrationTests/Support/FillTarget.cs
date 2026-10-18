using Edgeless.Core.Geometry;
using Edgeless.Interop;

namespace Edgeless.IntegrationTests.Support;

/// <summary>The fill target the applier is expected to use for a window on this machine's monitor layout.</summary>
internal static class FillTarget
{
    public static RectI For(nint hwnd)
        => FillOverhang.Compute(MonitorApi.FromWindow(hwnd).Bounds, [.. MonitorApi.EnumerateAll().Select(m => m.Bounds)], out _);
}
