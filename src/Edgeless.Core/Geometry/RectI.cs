namespace Edgeless.Core.Geometry;

/// <summary>Integer rectangle in physical pixels; Right/Bottom are exclusive like Win32 RECT.</summary>
public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public static RectI FromXywh(int x, int y, int width, int height) => new(x, y, x + width, y + height);
}
