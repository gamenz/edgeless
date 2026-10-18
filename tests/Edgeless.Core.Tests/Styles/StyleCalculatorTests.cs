using Edgeless.Core.Styles;

namespace Edgeless.Core.Tests.Styles;

public sealed class StyleCalculatorTests
{
    [Fact]
    public void ComputeBorderless_RemovesFrameStyles_KeepsOthers()
    {
        var original = new WindowStyles(StyleFlags.WS_OVERLAPPEDWINDOW | StyleFlags.WS_VISIBLE,
            StyleFlags.WS_EX_WINDOWEDGE | StyleFlags.WS_EX_CLIENTEDGE | StyleFlags.WS_EX_APPWINDOW);

        var result = StyleCalculator.ComputeBorderless(original, addPopupStyle: false);

        Assert.Equal(StyleFlags.WS_VISIBLE, result.Style);
        Assert.Equal(StyleFlags.WS_EX_APPWINDOW, result.ExStyle);
    }

    [Fact]
    public void ComputeBorderless_RemovesAllBorderExStyles()
    {
        var original = new WindowStyles(0, StyleFlags.WS_EX_DLGMODALFRAME | StyleFlags.WS_EX_STATICEDGE);

        Assert.Equal(0u, StyleCalculator.ComputeBorderless(original, false).ExStyle);
    }

    [Fact]
    public void ComputeBorderless_AddPopup_SetsPopup()
        => Assert.Equal(StyleFlags.WS_POPUP | StyleFlags.WS_VISIBLE,
            StyleCalculator.ComputeBorderless(new(StyleFlags.WS_OVERLAPPEDWINDOW | StyleFlags.WS_VISIBLE, 0), true).Style);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComputeBorderless_IsIdempotent(bool addPopupStyle)
    {
        var original = new WindowStyles(StyleFlags.WS_OVERLAPPEDWINDOW | StyleFlags.WS_VISIBLE, StyleFlags.WS_EX_CLIENTEDGE);
        var once = StyleCalculator.ComputeBorderless(original, addPopupStyle);

        Assert.Equal(once, StyleCalculator.ComputeBorderless(once, addPopupStyle));
    }

    [Fact]
    public void IsBorderless_TrueOnlyWhenNoFrameBitsAndPopupMatches()
    {
        Assert.False(StyleCalculator.IsBorderless(new(StyleFlags.WS_OVERLAPPEDWINDOW, 0), false));
        Assert.True(StyleCalculator.IsBorderless(new(StyleFlags.WS_VISIBLE, 0), false));
        Assert.False(StyleCalculator.IsBorderless(new(StyleFlags.WS_VISIBLE, 0), true));
        Assert.False(StyleCalculator.IsBorderless(new(StyleFlags.WS_VISIBLE, StyleFlags.WS_EX_CLIENTEDGE), false));
    }
}
