namespace Edgeless.Shell.Settings;

/// <summary>Control ids and positions in 96-DPI units; scaled to the window's DPI at layout time.</summary>
internal static class SettingsLayout
{
    public const int ClientWidth = 632;
    public const int ClientHeight = 436;

    public const int ProfileList = 100;
    public const int AddButton = 101;
    public const int RemoveButton = 102;
    public const int ExeEdit = 110;
    public const int EnabledCheck = 111;
    public const int MonitorCombo = 112;
    public const int SizeModeCombo = 113;
    public const int CustomX = 114;
    public const int CustomY = 115;
    public const int CustomWidth = 116;
    public const int CustomHeight = 117;
    public const int PopupCheck = 118;
    public const int DelayEdit = 119;
    public const int HotkeyControl = 130;
    public const int HotkeyWarning = 131;
    public const int AutostartCombo = 132;
    public const int OpenConfigButton = 140;
    public const int SaveButton = 141;
    public const int CancelButton = 142;

    /// <summary>Static labels: (text, x, y, width).</summary>
    public static readonly (string Text, int X, int Y, int Width)[] Labels =
    [
        ("Danh sách game", 12, 12, 200),
        ("Tên exe", 228, 14, 118),
        ("Màn hình", 228, 76, 118),
        ("Chế độ kích thước", 228, 108, 118),
        ("Tuỳ chỉnh x, y, w, h", 228, 140, 118),
        ("Delay áp dụng (ms)", 228, 202, 118),
        ("Hotkey", 228, 270, 118),
        ("Tự khởi động", 228, 326, 118),
    ];

    /// <summary>Control rectangles by id: (x, y, width, height). Combobox heights include the drop-down.</summary>
    public static readonly Dictionary<int, (int X, int Y, int Width, int Height)> Bounds = new()
    {
        [ProfileList] = (12, 34, 200, 300),
        [AddButton] = (12, 342, 96, 28),
        [RemoveButton] = (116, 342, 96, 28),
        [ExeEdit] = (350, 10, 270, 24),
        [EnabledCheck] = (350, 44, 270, 22),
        [MonitorCombo] = (350, 72, 270, 200),
        [SizeModeCombo] = (350, 104, 270, 200),
        [CustomX] = (350, 136, 62, 24),
        [CustomY] = (419, 136, 62, 24),
        [CustomWidth] = (488, 136, 62, 24),
        [CustomHeight] = (557, 136, 63, 24),
        [PopupCheck] = (350, 170, 270, 22),
        [DelayEdit] = (350, 198, 100, 24),
        [HotkeyControl] = (350, 266, 200, 24),
        [HotkeyWarning] = (350, 294, 270, 20),
        [AutostartCombo] = (350, 322, 270, 200),
        [OpenConfigButton] = (12, 394, 140, 30),
        [SaveButton] = (428, 394, 92, 30),
        [CancelButton] = (528, 394, 92, 30),
    };

    public static int Scale(int value, uint dpi) => (int)Math.Round(value * dpi / 96.0);
}
