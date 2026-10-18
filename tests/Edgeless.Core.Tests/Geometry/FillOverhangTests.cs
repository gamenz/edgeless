using Edgeless.Core.Geometry;

namespace Edgeless.Core.Tests.Geometry;

public sealed class FillOverhangTests
{
    private static readonly RectI Primary = new(0, 0, 2560, 1440);
    private static readonly RectI Below = new(0, 1440, 2560, 2880);
    private static readonly RectI Right = new(2560, 0, 5120, 1440);
    private static readonly RectI Above = new(0, -1440, 2560, 0);
    private static readonly RectI LeftOf = new(-2560, 0, 0, 1440);

    private static void AssertTarget(RectI expected, OverhangEdge expectedEdge, RectI monitor, params RectI[] all)
    {
        var target = FillOverhang.Compute(monitor, all, out var edge);

        Assert.Equal(expected, target);
        Assert.Equal(expectedEdge, edge);
    }

    [Fact]
    public void SingleMonitor_OverhangsBottom()
        => AssertTarget(new RectI(0, 0, 2560, 1441), OverhangEdge.Bottom, Primary, Primary);

    [Fact]
    public void MonitorBelow_OverhangsRight()
        => AssertTarget(new RectI(0, 0, 2561, 1440), OverhangEdge.Right, Primary, Primary, Below);

    [Fact]
    public void MonitorsBelowAndRight_OverhangsTop()
        => AssertTarget(new RectI(0, -1, 2560, 1440), OverhangEdge.Top, Primary, Primary, Below, Right);

    [Fact]
    public void MonitorsBelowRightAbove_OverhangsLeft()
        => AssertTarget(new RectI(-1, 0, 2560, 1440), OverhangEdge.Left, Primary, Primary, Below, Right, Above);

    [Fact]
    public void MonitorsOnAllSides_NoOverhang()
        => AssertTarget(Primary, OverhangEdge.None, Primary, Primary, Below, Right, Above, LeftOf);

    [Fact]
    public void OffsetMonitorBelow_BlocksBottom()
        => AssertTarget(new RectI(0, 0, 2561, 1440), OverhangEdge.Right, Primary, Primary, new RectI(1000, 1440, 3560, 2880));

    [Fact]
    public void DiagonalMonitor_DoesNotBlock()
        => AssertTarget(new RectI(0, 0, 2560, 1441), OverhangEdge.Bottom, Primary, Primary, new RectI(2560, 1440, 5120, 2880));

    [Fact]
    public void NegativeCoordinates_OverhangsInOwnSpace()
    {
        var left = new RectI(-1920, 200, 0, 1280);

        AssertTarget(new RectI(-1920, 200, 0, 1281), OverhangEdge.Bottom, left, Primary, left);
    }
}
