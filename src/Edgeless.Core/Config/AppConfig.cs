namespace Edgeless.Core.Config;

public enum SizeMode
{
    Fill,
    Center,
    Custom,
}

/// <summary>Custom window rect, relative to the selected monitor's top-left corner.</summary>
public sealed class CustomRect
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = 1920;

    public int Height { get; set; } = 1080;
}
