using Edgeless.Core.Config;
using Edgeless.Core.Geometry;
using static Edgeless.Shell.Settings.SettingsLayout;

namespace Edgeless.Shell.Settings;

/// <summary>Keeps the profile list and the detail fields in sync with <see cref="SettingsDraft.Profiles"/>.</summary>
internal sealed class ProfileEditor(SettingsDraft draft, IReadOnlyList<MonitorDescriptor> monitors, Func<int, nint> control)
{
    private static readonly string[] SizeModeLabels =
        ["fill — cover the monitor", "center — keep size, centered", "custom — custom position and size"];

    private static readonly int[] DetailControls =
        [ChangePathButton, EnabledCheck, MonitorCombo, SizeModeCombo, CustomX, CustomY, CustomWidth, CustomHeight, PopupCheck, DelayEdit];

    private List<MonitorOption> _monitorOptions = [];
    private int _selected = -1;

    public void Initialize()
    {
        Win32Controls.SetItems(control(SizeModeCombo), isCombo: true, SizeModeLabels);
        RefreshList(draft.Profiles.Count > 0 ? 0 : -1);
    }

    public void OnListSelectionChanged()
    {
        var index = Win32Controls.GetSelection(control(ProfileList), isCombo: false);
        if (index != _selected)
        {
            Commit();
            _selected = index;
            Load();
        }
    }

    public void OnSizeModeChanged() => UpdateCustomEnabled();

    /// <summary>Picks a game exe with the Open File dialog; an already listed path just gets selected.</summary>
    public void Add(nint owner)
    {
        Commit();
        var path = PickCanonical(owner, null);
        var index = path is null ? -1 : draft.AddProfile(path);
        if (index >= 0)
        {
            RefreshList(index);
        }
    }

    /// <summary>Points the selected profile at another exe; a path used by another profile is rejected.</summary>
    public void ChangePath(nint owner)
    {
        if (_selected < 0)
        {
            return;
        }

        Commit();
        var path = PickCanonical(owner, draft.Profiles[_selected].Path);
        if (path is not null && draft.ReplacePath(_selected, path))
        {
            RefreshList(_selected);
        }
    }

    /// <summary>Open File dialog result resolved through subst drives / junctions, like process paths.</summary>
    private static string? PickCanonical(nint owner, string? initialPath)
        => FileDialog.PickExe(owner, initialPath) is { } path ? Platform.PathCanonicalizer.Canonicalize(path) : null;

    public void Remove()
    {
        if (_selected < 0)
        {
            return;
        }

        var next = draft.RemoveAt(_selected);
        _selected = -1;
        RefreshList(next);
    }

    /// <summary>Writes the detail fields back into the selected profile.</summary>
    public void Commit()
    {
        if (_selected < 0 || _selected >= draft.Profiles.Count)
        {
            return;
        }

        var p = draft.Profiles[_selected];
        p.Enabled = Win32Controls.GetCheck(control(EnabledCheck));
        var monitor = Win32Controls.GetSelection(control(MonitorCombo), isCombo: true);
        p.Monitor = monitor >= 0 && monitor < _monitorOptions.Count ? _monitorOptions[monitor].Value : p.Monitor;
        p.SizeMode = (SizeMode)Math.Max(0, Win32Controls.GetSelection(control(SizeModeCombo), isCombo: true));
        p.Custom.X = SettingsDraft.ParseInt(Win32Controls.GetText(control(CustomX)), p.Custom.X);
        p.Custom.Y = SettingsDraft.ParseInt(Win32Controls.GetText(control(CustomY)), p.Custom.Y);
        p.Custom.Width = SettingsDraft.ParseInt(Win32Controls.GetText(control(CustomWidth)), p.Custom.Width);
        p.Custom.Height = SettingsDraft.ParseInt(Win32Controls.GetText(control(CustomHeight)), p.Custom.Height);
        p.AddPopupStyle = Win32Controls.GetCheck(control(PopupCheck));
        p.ApplyDelayMs = SettingsDraft.ParseInt(Win32Controls.GetText(control(DelayEdit)), p.ApplyDelayMs);
    }

    /// <summary>Current DPI of the settings window; used to size the list's horizontal scroll range.</summary>
    public uint Dpi { get; set; } = 96;

    /// <summary>Lets long labels scroll horizontally (approximate width, generous so nothing is cut).</summary>
    public void UpdateListExtent()
    {
        var longest = draft.Profiles.Select(SettingsDraft.ListLabel).Select(l => l.Length).DefaultIfEmpty(0).Max();
        Win32Controls.SetHorizontalExtent(control(ProfileList), Scale(longest * AverageCharWidth, Dpi));
    }

    private const int AverageCharWidth = 8; // 96-DPI pixels per character of the UI font, rounded up

    private void RefreshList(int select)
    {
        Win32Controls.SetItems(control(ProfileList), isCombo: false, draft.Profiles.Select(SettingsDraft.ListLabel));
        UpdateListExtent();
        Win32Controls.SetSelection(control(ProfileList), isCombo: false, select);
        _selected = select;
        Load();
    }

    private void Load()
    {
        var hasProfile = _selected >= 0 && _selected < draft.Profiles.Count;
        foreach (var id in DetailControls)
        {
            Win32Controls.Enable(control(id), hasProfile);
        }

        var p = hasProfile ? draft.Profiles[_selected] : new GameProfile();
        _monitorOptions = SettingsDraft.MonitorOptions(monitors);
        var monitorIndex = SettingsDraft.MonitorIndex(_monitorOptions, p.Monitor);
        Win32Controls.SetItems(control(MonitorCombo), isCombo: true, _monitorOptions.Select(o => o.Label));
        Win32Controls.SetSelection(control(MonitorCombo), isCombo: true, monitorIndex);

        Win32Controls.SetText(control(PathEdit), hasProfile ? p.Path : "");
        Win32Controls.SetCheck(control(EnabledCheck), p.Enabled);
        Win32Controls.SetSelection(control(SizeModeCombo), isCombo: true, (int)p.SizeMode);
        Win32Controls.SetText(control(CustomX), p.Custom.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Win32Controls.SetText(control(CustomY), p.Custom.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Win32Controls.SetText(control(CustomWidth), p.Custom.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Win32Controls.SetText(control(CustomHeight), p.Custom.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Win32Controls.SetCheck(control(PopupCheck), p.AddPopupStyle);
        Win32Controls.SetText(control(DelayEdit), p.ApplyDelayMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Win32Controls.Enable(control(RemoveButton), hasProfile);
        UpdateCustomEnabled();
    }

    private void UpdateCustomEnabled()
    {
        var isCustom = _selected >= 0
            && Win32Controls.GetSelection(control(SizeModeCombo), isCombo: true) == (int)SizeMode.Custom;
        foreach (var id in (int[])[CustomX, CustomY, CustomWidth, CustomHeight])
        {
            Win32Controls.Enable(control(id), isCustom);
        }
    }
}
