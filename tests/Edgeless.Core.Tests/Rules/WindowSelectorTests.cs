using Edgeless.Core.Geometry;
using Edgeless.Core.Rules;
using Edgeless.Core.Styles;

namespace Edgeless.Core.Tests.Rules;

public sealed class WindowSelectorTests
{
    private static WindowCandidate Make(nint hwnd, int width, int height, uint style = StyleFlags.WS_VISIBLE,
        uint exStyle = 0, bool isRoot = true, bool hasOwner = false, bool isVisible = true, bool isCloaked = false)
        => new(hwnd, isRoot, hasOwner, isVisible, isCloaked, new WindowStyles(style, exStyle), RectI.FromXywh(0, 0, width, height));

    [Fact]
    public void SelectLargest_SkipsIneligible_PicksLargestArea()
    {
        WindowCandidate[] candidates =
        [
            Make(1, 800, 600),
            Make(2, 2000, 2000, exStyle: StyleFlags.WS_EX_TOOLWINDOW),
            Make(3, 2000, 2000, isCloaked: true),
            Make(4, 2000, 2000, hasOwner: true),
            Make(5, 2000, 2000, isRoot: false),
            Make(6, 2000, 2000, isVisible: false),
            Make(7, 2000, 2000, style: StyleFlags.WS_CHILD | StyleFlags.WS_VISIBLE),
            Make(8, 1280, 720),
        ];

        Assert.Equal((nint)8, WindowSelector.SelectLargest(candidates));
    }

    [Fact]
    public void SelectLargest_TieKeepsFirst()
        => Assert.Equal((nint)1, WindowSelector.SelectLargest([Make(1, 100, 100), Make(2, 100, 100)]));

    [Fact]
    public void SelectLargest_NoEligible_ReturnsZero()
        => Assert.Equal((nint)0, WindowSelector.SelectLargest([Make(1, 100, 100, isCloaked: true)]));
}
