using System.Globalization;
using Edgeless.Core.Config;
using Edgeless.Core.Geometry;
using Edgeless.Core.Rules;

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

    /// <summary>
    /// Adds a profile for <paramref name="path"/>. Returns its index, the existing index when that path is already
    /// listed, or -1 when the path is not a valid full path.
    /// </summary>
    public int AddProfile(string path)
    {
        var normalized = ExePath.Normalize(path);
        if (normalized.Length == 0)
        {
            return -1;
        }

        var existing = Profiles.FindIndex(p => ExePath.AreSame(p.Path, normalized));
        if (existing >= 0)
        {
            return existing;
        }

        Profiles.Add(GameProfile.CreateDefault(normalized));
        return Profiles.Count - 1;
    }

    /// <summary>Points a profile at another exe. False (nothing changed) when invalid or used by another profile.</summary>
    public bool ReplacePath(int index, string path)
    {
        var normalized = ExePath.Normalize(path);
        var clash = Profiles.Where((p, i) => i != index && ExePath.AreSame(p.Path, normalized)).Any();
        if (normalized.Length == 0 || clash)
        {
            return false;
        }

        Profiles[index].Path = normalized;
        return true;
    }

    /// <summary>List text "file.exe — folder"; deep folders keep the root and the last two levels ("D:\…\bin\Debug").</summary>
    public static string ListLabel(GameProfile profile)
    {
        var folder = System.IO.Path.GetDirectoryName(profile.Path) ?? "";
        var root = System.IO.Path.GetPathRoot(folder) ?? "";
        var levels = folder[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var shortFolder = levels.Length <= 2
            ? folder
            : $"{root.TrimEnd('\\')}\\…\\{levels[^2]}\\{levels[^1]}";
        return $"{profile.ExeName} — {shortFolder}";
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
