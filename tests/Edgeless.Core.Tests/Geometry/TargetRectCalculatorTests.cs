using Edgeless.Core.Config;
using Edgeless.Core.Geometry;

namespace Edgeless.Core.Tests.Geometry;

public sealed class TargetRectCalculatorTests
{
    private static readonly MonitorDescriptor Primary = new(@"\\.\DISPLAY1", new RectI(0, 0, 2560, 1440));
    private static readonly MonitorDescriptor Left = new(@"\\.\DISPLAY2", new RectI(-1920, 200, 0, 1280));
    private static readonly MonitorDescriptor[] All = [Primary, Left];

    [Fact]
    public void SelectMonitor_Current_ReturnsCurrent()
    {
        var result = TargetRectCalculator.SelectMonitor(TargetRectCalculator.CurrentMonitor, Left, All, out var fellBack);

        Assert.Equal(Left, result);
        Assert.False(fellBack);
    }

    [Fact]
    public void SelectMonitor_ByDeviceName_IgnoresCase()
    {
        var result = TargetRectCalculator.SelectMonitor(@"\\.\display2", Primary, All, out var fellBack);

        Assert.Equal(Left, result);
        Assert.False(fellBack);
    }

    [Fact]
    public void SelectMonitor_MissingDevice_FallsBackToCurrent()
    {
        var result = TargetRectCalculator.SelectMonitor(@"\\.\DISPLAY9", Primary, All, out var fellBack);

        Assert.Equal(Primary, result);
        Assert.True(fellBack);
    }

    private static readonly RectI[] AllBounds = [.. All.Select(m => m.Bounds)];

    [Fact]
    public void Fill_UsesOverhang()
    {
        var target = TargetRectCalculator.ComputeTarget(SizeMode.Fill, Left.Bounds, AllBounds, 800, 600, new(), out var edge);

        Assert.Equal(new RectI(-1920, 200, 0, 1281), target);
        Assert.Equal(OverhangEdge.Bottom, edge);
    }

    [Fact]
    public void Center_KeepsClientSize_CentersOnMonitor()
    {
        var target = TargetRectCalculator.ComputeTarget(SizeMode.Center, Left.Bounds, AllBounds, 800, 540, new(), out var edge);

        Assert.Equal(new RectI(-1360, 470, -560, 1010), target);
        Assert.Equal(OverhangEdge.None, edge);
    }

    [Fact]
    public void Center_ClientLargerThanMonitor_ClampsToMonitor()
        => Assert.Equal(Primary.Bounds,
            TargetRectCalculator.ComputeTarget(SizeMode.Center, Primary.Bounds, AllBounds, 4000, 3000, new(), out _));

    [Fact]
    public void Custom_IsRelativeToMonitorOrigin()
    {
        var target = TargetRectCalculator.ComputeTarget(SizeMode.Custom, Left.Bounds, AllBounds, 0, 0,
            new CustomRect { X = 100, Y = 50, Width = 800, Height = 600 }, out var edge);

        Assert.Equal(new RectI(-1820, 250, -1020, 850), target);
        Assert.Equal(OverhangEdge.None, edge);
    }

    [Fact]
    public void RectI_FromXywh_ComputesEdgesAndSize()
    {
        var rect = RectI.FromXywh(-10, 20, 300, 400);

        Assert.Equal(new RectI(-10, 20, 290, 420), rect);
        Assert.Equal(300, rect.Width);
        Assert.Equal(400, rect.Height);
    }
}
