namespace Edgeless.Core.Geometry;

/// <summary>The monitor edge a fill target extends 1 px beyond; None when every edge has a neighbor monitor.</summary>
public enum OverhangEdge
{
    None,
    Bottom,
    Right,
    Top,
    Left,
}

/// <summary>
/// Fill target = the monitor plus 1 px beyond one free edge, so it never equals a size the game itself can request
/// (spec docs/specs/2026-10-19-fill-overhang-spec.md).
/// </summary>
public static class FillOverhang
{
    private static readonly OverhangEdge[] EdgeOrder = [OverhangEdge.Bottom, OverhangEdge.Right, OverhangEdge.Top, OverhangEdge.Left];

    public static RectI Compute(RectI monitor, IReadOnlyList<RectI> allMonitors, out OverhangEdge edge)
    {
        foreach (var candidate in EdgeOrder)
        {
            var strip = Strip(monitor, candidate);
            if (!allMonitors.Any(m => m.Intersects(strip)))
            {
                edge = candidate;
                return new RectI(Math.Min(monitor.Left, strip.Left), Math.Min(monitor.Top, strip.Top),
                    Math.Max(monitor.Right, strip.Right), Math.Max(monitor.Bottom, strip.Bottom));
            }
        }

        edge = OverhangEdge.None;
        return monitor;
    }

    /// <summary>The 1 px strip just outside <paramref name="monitor"/> along <paramref name="edge"/>.</summary>
    private static RectI Strip(RectI monitor, OverhangEdge edge) => edge switch
    {
        OverhangEdge.Bottom => monitor with { Top = monitor.Bottom, Bottom = monitor.Bottom + 1 },
        OverhangEdge.Right => monitor with { Left = monitor.Right, Right = monitor.Right + 1 },
        OverhangEdge.Top => monitor with { Top = monitor.Top - 1, Bottom = monitor.Top },
        _ => monitor with { Left = monitor.Left - 1, Right = monitor.Left },
    };
}
