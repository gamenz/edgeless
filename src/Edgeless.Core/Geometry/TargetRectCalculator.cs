using Edgeless.Core.Config;

namespace Edgeless.Core.Geometry;

public static class TargetRectCalculator
{
    public const string CurrentMonitor = "current";

    /// <summary>
    /// Resolves the configured monitor. "current" or an unknown device name yields <paramref name="current"/>;
    /// <paramref name="fellBack"/> is true only when a device name was given but not found.
    /// </summary>
    public static MonitorDescriptor SelectMonitor(
        string monitorSetting, MonitorDescriptor current, IReadOnlyList<MonitorDescriptor> all, out bool fellBack)
    {
        fellBack = false;
        if (string.Equals(monitorSetting, CurrentMonitor, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        foreach (var monitor in all)
        {
            if (string.Equals(monitor.DeviceName, monitorSetting, StringComparison.OrdinalIgnoreCase))
            {
                return monitor;
            }
        }

        fellBack = true;
        return current;
    }

    public static RectI ComputeTarget(SizeMode mode, RectI monitorBounds, int clientWidth, int clientHeight, CustomRect custom)
    {
        switch (mode)
        {
            case SizeMode.Center:
                var width = Math.Min(clientWidth, monitorBounds.Width);
                var height = Math.Min(clientHeight, monitorBounds.Height);
                return RectI.FromXywh(
                    monitorBounds.Left + (monitorBounds.Width - width) / 2,
                    monitorBounds.Top + (monitorBounds.Height - height) / 2,
                    width,
                    height);
            case SizeMode.Custom:
                return RectI.FromXywh(monitorBounds.Left + custom.X, monitorBounds.Top + custom.Y, custom.Width, custom.Height);
            default:
                return monitorBounds;
        }
    }
}
