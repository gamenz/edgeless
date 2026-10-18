namespace Edgeless.Core.Styles;

/// <summary>A window's GWL_STYLE and GWL_EXSTYLE values.</summary>
public readonly record struct WindowStyles(uint Style, uint ExStyle);

public static class StyleCalculator
{
    /// <summary>Strips frame styles; WS_EX_APPWINDOW and all unrelated bits are kept.</summary>
    public static WindowStyles ComputeBorderless(WindowStyles original, bool addPopupStyle)
    {
        var style = original.Style & ~StyleFlags.BorderStyleMask;
        if (addPopupStyle)
        {
            style |= StyleFlags.WS_POPUP;
        }

        return new WindowStyles(style, original.ExStyle & ~StyleFlags.BorderExStyleMask);
    }

    public static bool IsBorderless(WindowStyles current, bool addPopupStyle)
        => current == ComputeBorderless(current, addPopupStyle);
}
