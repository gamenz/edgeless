using System.Globalization;
using Edgeless.Core.Config;
using Edgeless.Core.Geometry;

namespace Edgeless.Shell.Settings;

internal sealed record MonitorOption(string Label, string Value);

/// <summary>The settings window's editable copy of the config, plus the pure conversions the window needs.</summary>
internal sealed class SettingsDraft
{
    private const uint HOTKEYF_SHIFT = 0x1;
    private const uint HOTKEYF_CONTROL = 0x2;
    private const uint HOTKEYF_ALT = 0x4;

    private readonly AppConfig _copy;

    public SettingsDraft(AppConfig current)
    {
        // Round-trip through JSON for a deep copy; the live config is untouched until Save.
        _copy = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(current));
    }

    public List<GameProfile> Profiles => _copy.Profiles;

    public HotkeyConfig Hotkey
    {
        get => _copy.Hotkey;
        set => _copy.Hotkey = value;
    }

    public AutostartMode Autostart
    {
        get => _copy.Autostart;
        set => _copy.Autostart = value;
    }

    /// <summary>Adds a placeholder profile with a unique exe name; returns its index.</summary>
    public int AddProfile()
    {
        var name = "game.exe";
        for (var n = 2; Profiles.Any(p => p.Exe == name); n++)
        {
            name = $"game{n}.exe";
        }

        Profiles.Add(GameProfile.CreateDefault(name));
        return Profiles.Count - 1;
    }

    /// <summary>Removes a profile; returns the index to select next, or -1 when the list is empty.</summary>
    public int RemoveAt(int index)
    {
        Profiles.RemoveAt(index);
        return Profiles.Count == 0 ? -1 : Math.Min(index, Profiles.Count - 1);
    }

    /// <summary>A fresh, normalized config built from the draft.</summary>
    public AppConfig ToConfig() => ConfigSerializer.Normalize(ConfigSerializer.Deserialize(ConfigSerializer.Serialize(_copy)));

    public static List<MonitorOption> MonitorOptions(IEnumerable<MonitorDescriptor> monitors)
        => [new("Màn hình hiện tại", TargetRectCalculator.CurrentMonitor),
            .. monitors.Select(m => new MonitorOption($"{m.DeviceName} — {m.Bounds.Width}×{m.Bounds.Height}", m.DeviceName))];

    /// <summary>Index of <paramref name="value"/>; a disconnected device is appended so saving does not lose it.</summary>
    public static int MonitorIndex(List<MonitorOption> options, string value)
    {
        var index = options.FindIndex(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            return index;
        }

        options.Add(new MonitorOption($"{value} (không kết nối)", value));
        return options.Count - 1;
    }

    /// <summary>msctls_hotkey32 value (HIBYTE = HOTKEYF_* flags, LOBYTE = virtual key) to a hotkey config.</summary>
    public static HotkeyConfig HotkeyFromControl(ushort value)
    {
        uint flags = (uint)value >> 8;
        uint modifiers = ((flags & HOTKEYF_CONTROL) != 0 ? HotkeyBinding.MOD_CONTROL : 0)
            | ((flags & HOTKEYF_ALT) != 0 ? HotkeyBinding.MOD_ALT : 0)
            | ((flags & HOTKEYF_SHIFT) != 0 ? HotkeyBinding.MOD_SHIFT : 0);
        return HotkeyBinding.FromNative(modifiers, (uint)value & 0xFF);
    }

    /// <summary>Hotkey config to an msctls_hotkey32 value; 0 when it cannot be represented.</summary>
    public static ushort HotkeyToControl(HotkeyConfig hotkey)
    {
        if (!HotkeyBinding.TryParse(hotkey, out var modifiers, out var virtualKey))
        {
            return 0;
        }

        var flags = ((modifiers & HotkeyBinding.MOD_CONTROL) != 0 ? HOTKEYF_CONTROL : 0)
            | ((modifiers & HotkeyBinding.MOD_ALT) != 0 ? HOTKEYF_ALT : 0)
            | ((modifiers & HotkeyBinding.MOD_SHIFT) != 0 ? HOTKEYF_SHIFT : 0);
        return (ushort)((flags << 8) | virtualKey);
    }

    public static int ParseInt(string text, int fallback)
        => int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
