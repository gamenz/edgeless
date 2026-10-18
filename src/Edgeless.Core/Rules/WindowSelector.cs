using Edgeless.Core.Geometry;
using Edgeless.Core.Styles;

namespace Edgeless.Core.Rules;

/// <summary>Facts about one top-level window, gathered by the interop layer.</summary>
public readonly record struct WindowCandidate(
    nint Hwnd, bool IsRoot, bool HasOwner, bool IsVisible, bool IsCloaked, WindowStyles Styles, RectI Bounds);

public static class WindowSelector
{
    public static bool IsEligible(in WindowCandidate c)
        => c.IsRoot
            && !c.HasOwner
            && c.IsVisible
            && !c.IsCloaked
            && (c.Styles.Style & StyleFlags.WS_CHILD) == 0
            && (c.Styles.ExStyle & StyleFlags.WS_EX_TOOLWINDOW) == 0;

    /// <summary>Hwnd of the eligible candidate with the largest area; first wins ties; 0 when none.</summary>
    public static nint SelectLargest(IEnumerable<WindowCandidate> candidates)
    {
        nint best = 0;
        long bestArea = -1;
        foreach (var candidate in candidates)
        {
            if (!IsEligible(candidate))
            {
                continue;
            }

            var area = (long)candidate.Bounds.Width * candidate.Bounds.Height;
            if (area > bestArea)
            {
                best = candidate.Hwnd;
                bestArea = area;
            }
        }

        return best;
    }
}
