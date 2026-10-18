using System.Text.Json.Serialization;
using Edgeless.Core.Rules;

namespace Edgeless.Core.Config;

public enum SizeMode
{
    [JsonStringEnumMemberName("fill")]
    Fill,

    [JsonStringEnumMemberName("center")]
    Center,

    [JsonStringEnumMemberName("custom")]
    Custom,
}

public enum AutostartMode
{
    [JsonStringEnumMemberName("off")]
    Off,

    [JsonStringEnumMemberName("normal")]
    Normal,

    [JsonStringEnumMemberName("elevated")]
    Elevated,
}

/// <summary>Root of config.json (spec section 4.11).</summary>
public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public HotkeyConfig Hotkey { get; set; } = new();

    public AutostartMode Autostart { get; set; } = AutostartMode.Off;

    public List<GameProfile> Profiles { get; set; } = [];
}

public sealed class HotkeyConfig
{
    public List<string> Modifiers { get; set; } = ["Ctrl", "Alt"];

    public string Key { get; set; } = "B";
}

public sealed class GameProfile
{
    public const int DefaultApplyDelayMs = 500;

    public string Exe { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public string Monitor { get; set; } = "current";

    public SizeMode SizeMode { get; set; } = SizeMode.Fill;

    public CustomRect Custom { get; set; } = new();

    public bool AddPopupStyle { get; set; }

    public int ApplyDelayMs { get; set; } = DefaultApplyDelayMs;

    public static GameProfile CreateDefault(string exe) => new() { Exe = ExeNameMatcher.Normalize(exe) };
}

/// <summary>Custom window rect, relative to the selected monitor's top-left corner.</summary>
public sealed class CustomRect
{
    public const int DefaultWidth = 1920;
    public const int DefaultHeight = 1080;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = DefaultWidth;

    public int Height { get; set; } = DefaultHeight;
}
