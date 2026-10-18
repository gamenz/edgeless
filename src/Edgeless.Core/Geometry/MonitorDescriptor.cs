namespace Edgeless.Core.Geometry;

/// <summary>A display monitor: its device name (e.g. \.\DISPLAY1) and full bounds (rcMonitor).</summary>
public sealed record MonitorDescriptor(string DeviceName, RectI Bounds);
