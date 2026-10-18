using Edgeless.Core.Config;
using Edgeless.Core.Geometry;
using Edgeless.Core.Logging;
using Edgeless.Core.Styles;
using Edgeless.Interop;

namespace Edgeless.Windowing;

internal enum ApplyResult
{
    Applied,
    AlreadyBorderless,
    SkippedMinimized,
    AccessDenied,
    Failed,
}

/// <summary>Applies (spec section 4.2) and restores (section 4.3) borderless on one foreign window.</summary>
internal sealed class BorderlessApplier(WindowRegistry registry, ILog log)
{
    private const int ErrorAccessDenied = 5;

    public ApplyResult Apply(WindowInfo window, GameProfile profile)
    {
        var hwnd = window.Hwnd;
        if (!ForeignWindow.IsWindow(hwnd))
        {
            return ApplyResult.Failed;
        }

        if (ForeignWindow.IsMinimized(hwnd))
        {
            return ApplyResult.SkippedMinimized;
        }

        var known = registry.TryGet(hwnd, out var managed) && managed.Pid == window.Pid;
        var originalStyles = known ? managed.OriginalStyles : ForeignWindow.GetStyles(hwnd);
        var originalPlacement = known ? managed.OriginalPlacement : ForeignWindow.GetPlacement(hwnd);

        if (ForeignWindow.IsMaximized(hwnd))
        {
            ForeignWindow.Restore(hwnd);
        }

        var current = ForeignWindow.GetStyles(hwnd);
        var target = ComputeTarget(window, profile);
        var oldRect = ForeignWindow.GetWindowRect(hwnd);
        if (StyleCalculator.IsBorderless(current, profile.AddPopupStyle) && oldRect == target)
        {
            return ApplyResult.AlreadyBorderless;
        }

        var error = ForeignWindow.SetStyles(hwnd, StyleCalculator.ComputeBorderless(current, profile.AddPopupStyle));
        if (error == 0)
        {
            error = ForeignWindow.SetPosition(hwnd, target);
        }

        if (error != 0)
        {
            log.Warn($"Apply failed: {window.ExeName} pid={window.Pid} hwnd=0x{hwnd:X} error={error}");
            return error == ErrorAccessDenied ? ApplyResult.AccessDenied : ApplyResult.Failed;
        }

        if (known)
        {
            managed.Profile = profile;
        }
        else
        {
            registry.Add(new ManagedWindow
            {
                Hwnd = hwnd, Pid = window.Pid, ImagePath = window.ImagePath,
                OriginalStyles = originalStyles, OriginalPlacement = originalPlacement, Profile = profile,
            });
        }

        log.Info($"Applied: {window.ExeName} pid={window.Pid} hwnd=0x{hwnd:X} {Format(oldRect)} -> {Format(target)}");
        return ApplyResult.Applied;
    }

    /// <summary>Restores the original state. Returns false when not managed or the window/process is gone.</summary>
    public bool Restore(nint hwnd)
    {
        if (!registry.TryGet(hwnd, out var managed))
        {
            return false;
        }

        registry.Remove(hwnd);
        if (!ForeignWindow.IsWindow(hwnd) || ForeignWindow.GetProcessId(hwnd) != managed.Pid)
        {
            log.Info($"Restore skipped (window gone): {managed.ExeName} pid={managed.Pid} hwnd=0x{hwnd:X}");
            return false;
        }

        var error = ForeignWindow.SetStyles(hwnd, managed.OriginalStyles);
        ForeignWindow.SetPlacement(hwnd, managed.OriginalPlacement);
        ForeignWindow.RedrawFrame(hwnd);
        log.Info($"Restored: {managed.ExeName} pid={managed.Pid} hwnd=0x{hwnd:X} error={error}");
        return error == 0;
    }

    public void RestoreAll()
    {
        foreach (var hwnd in registry.All.Select(w => w.Hwnd).ToList())
        {
            Restore(hwnd);
        }
    }

    private RectI ComputeTarget(WindowInfo window, GameProfile profile)
    {
        var current = MonitorApi.FromWindow(window.Hwnd);
        var monitor = TargetRectCalculator.SelectMonitor(profile.Monitor, current, MonitorApi.EnumerateAll(), out var fellBack);
        if (fellBack)
        {
            log.Warn($"Monitor '{profile.Monitor}' not found for {window.ExeName}; using {current.DeviceName}");
        }

        var (width, height) = ForeignWindow.GetClientSize(window.Hwnd);
        return TargetRectCalculator.ComputeTarget(profile.SizeMode, monitor.Bounds, width, height, profile.Custom);
    }

    private static string Format(RectI r) => $"({r.Left},{r.Top} {r.Width}x{r.Height})";
}
