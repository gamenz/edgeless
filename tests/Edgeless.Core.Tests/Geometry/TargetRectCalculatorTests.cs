using Edgeless.Core.Config;
using Edgeless.Core.Geometry;

namespace Edgeless.Core.Tests.Geometry;

public sealed class TargetRectCalculatorTests
{
    private static readonly MonitorDescriptor Primary = new(@"\.\DISPLAY1", new RectI(0, 0, 2560, 1440));
    private static readonly MonitorDescriptor Left = new(@"\.\DISPLAY2", new RectI(-1920, 200, 0, 1280));
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
        var result = TargetRectCalculator.SelectMonitor(@"\.\display2", Primary, All, out var fellBack);

        Assert.Equal(Left, result);
        Assert.False(fellBack);
    }

    [Fact]
    public void SelectMonitor_MissingDevice_FallsBackToCurrent()
    {
        var result = TargetRectCalculator.SelectMonitor(@"\.\DISPLAY9", Primary, All, out var fellBack);

        Assert.Equal(Primary, result);
        Assert.True(fellBack);
    }

    [Fact]
    public void Fill_NegativeCoordinates_EqualsMonitorBounds()
        => Assert.Equal(Left.Bounds, TargetRectCalculator.ComputeTarget(SizeMode.Fill, Left.Bounds, 800, 600, new()));

    [Fact]
    public void Center_KeepsClientSize_CentersOnMonitor()
        => Assert.Equal(new RectI(-1360, 470, -560, 1010),
            TargetRectCalculator.ComputeTarget(SizeMode.Center, Left.Bounds, 800, 540, new()));

    [Fact]
    public void Center_ClientLargerThanMonitor_ClampsToMonitor()
        => Assert.Equal(Primary.Bounds, TargetRectCalculator.ComputeTarget(SizeMode.Center, Primary.Bounds, 4000, 3000, new()));

    [Fact]
    public void Custom_IsRelativeToMonitorOrigin()
        => Assert.Equal(new RectI(-1820, 250, -1020, 850),
            TargetRectCalculator.ComputeTarget(SizeMode.Custom, Left.Bounds, 0, 0,
                new CustomRect { X = 100, Y = 50, Width = 800, Height = 600 }));

    [Fact]
    public void RectI_FromXywh_ComputesEdgesAndSize()
    {
        var rect = RectI.FromXywh(-10, 20, 300, 400);

        Assert.Equal(new RectI(-10, 20, 290, 420), rect);
        Assert.Equal(300, rect.Width);
        Assert.Equal(400, rect.Height);
    }
}
