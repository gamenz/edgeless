using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Edgeless.Core.Config;
using Edgeless.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using static Edgeless.Shell.Settings.SettingsLayout;

namespace Edgeless.Shell.Settings;

internal sealed record SettingsSaveResult(bool HotkeyRegistered);

/// <summary>The plain Win32 settings window (spec section 4.8). One instance at a time, UI thread only.</summary>
internal sealed unsafe class SettingsWindow
{
    private const string ClassName = "Edgeless.SettingsWindow";
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DPICHANGED = 0x02E0;
    private const uint SPI_GETNONCLIENTMETRICS = 0x0029;
    private const int ColorBtnFace = 15;
    private const int CwUseDefault = unchecked((int)0x80000000);
    private static readonly string[] AutostartLabels = ["Tắt", "Bình thường", "Với quyền admin"];

    private static SettingsWindow? _current;
    private static bool _classRegistered;

    private readonly Func<AppConfig, SettingsSaveResult> _onSave;
    private readonly Action _openConfig;
    private readonly SettingsDraft _draft;
    private readonly ProfileEditor _editor;
    private readonly Dictionary<int, nint> _controls = [];
    private readonly List<nint> _labels = [];
    private readonly nint _hwnd;
    private HFONT _font;

    private SettingsWindow(AppConfig current, Func<AppConfig, SettingsSaveResult> onSave, Action openConfig)
    {
        _onSave = onSave;
        _openConfig = openConfig;
        _draft = new SettingsDraft(current);
        RegisterClassOnce();
        fixed (char* cls = ClassName)
        fixed (char* title = "Edgeless — Cài đặt")
        {
            _hwnd = PInvoke.CreateWindowEx(WINDOW_EX_STYLE.WS_EX_CONTROLPARENT, cls, title,
                WINDOW_STYLE.WS_OVERLAPPED | WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_SYSMENU | WINDOW_STYLE.WS_MINIMIZEBOX,
                CwUseDefault, CwUseDefault, 640, 480, HWND.Null, HMENU.Null,
                PInvoke.GetModuleHandle((PCWSTR)null), null);
        }

        _current = this;
        CreateControls();
        _editor = new ProfileEditor(_draft, MonitorApi.EnumerateAll(), id => _controls[id]);
        _editor.Initialize();
        Win32Controls.SetHotkey(_controls[HotkeyControl], SettingsDraft.HotkeyToControl(_draft.Hotkey));
        Win32Controls.SetItems(_controls[AutostartCombo], isCombo: true, AutostartLabels);
        Win32Controls.SetSelection(_controls[AutostartCombo], isCombo: true, (int)_draft.Autostart);
        Layout();
        MessageWindow.RegisterDialog(_hwnd);
        PInvoke.ShowWindow(new HWND(_hwnd), SHOW_WINDOW_CMD.SW_SHOW);
        PInvoke.SetForegroundWindow(new HWND(_hwnd));
    }

    public static void ShowOrActivate(AppConfig current, Func<AppConfig, SettingsSaveResult> onSave, Action openConfig)
    {
        if (_current is not null)
        {
            PInvoke.ShowWindow(new HWND(_current._hwnd), SHOW_WINDOW_CMD.SW_RESTORE);
            PInvoke.SetForegroundWindow(new HWND(_current._hwnd));
            return;
        }

        _ = new SettingsWindow(current, onSave, openConfig);
    }

    private void CreateControls()
    {
        foreach (var label in Labels)
        {
            _labels.Add(Win32Controls.Create(_hwnd, "STATIC", label.Text, 0, 0));
        }

        const uint Tab = Win32Controls.WS_TABSTOP;
        const uint Combo = Tab | Win32Controls.CBS_DROPDOWNLIST | Win32Controls.WS_VSCROLL;
        const uint Edit = Tab | Win32Controls.ES_AUTOHSCROLL;
        Add(ProfileList, "LISTBOX", "", Tab | Win32Controls.LBS_NOTIFY | Win32Controls.LBS_NOINTEGRALHEIGHT
            | Win32Controls.WS_VSCROLL, Win32Controls.WS_EX_CLIENTEDGE);
        Add(AddButton, "BUTTON", "Thêm", Tab);
        Add(RemoveButton, "BUTTON", "Xoá", Tab);
        Add(ExeEdit, "EDIT", "", Edit, Win32Controls.WS_EX_CLIENTEDGE);
        Add(EnabledCheck, "BUTTON", "Bật", Tab | Win32Controls.BS_AUTOCHECKBOX);
        Add(MonitorCombo, "COMBOBOX", "", Combo);
        Add(SizeModeCombo, "COMBOBOX", "", Combo);
        foreach (var id in (int[])[CustomX, CustomY, CustomWidth, CustomHeight])
        {
            Add(id, "EDIT", "", Edit, Win32Controls.WS_EX_CLIENTEDGE);
        }

        Add(PopupCheck, "BUTTON", "Thêm WS_POPUP", Tab | Win32Controls.BS_AUTOCHECKBOX);
        Add(DelayEdit, "EDIT", "", Edit, Win32Controls.WS_EX_CLIENTEDGE);
        Add(HotkeyControl, "msctls_hotkey32", "", Tab);
        Add(HotkeyWarning, "STATIC", "", 0);
        Add(AutostartCombo, "COMBOBOX", "", Combo);
        Add(OpenConfigButton, "BUTTON", "Mở config.json", Tab);
        Add(SaveButton, "BUTTON", "Lưu", Tab | Win32Controls.BS_DEFPUSHBUTTON);
        Add(CancelButton, "BUTTON", "Huỷ", Tab);

        void Add(int id, string cls, string text, uint style, uint exStyle = 0)
            => _controls[id] = Win32Controls.Create(_hwnd, cls, text, style, id, exStyle);
    }

    /// <summary>Positions every control and sizes the window for the current DPI, and refreshes the font.</summary>
    private void Layout()
    {
        // Per-monitor DPI APIs need Windows 10 1607+, below the spec's minimum; the guard satisfies the analyzer.
        uint dpi = 96;
        var oldFont = _font;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            dpi = PInvoke.GetDpiForWindow(new HWND(_hwnd));
            var metrics = new NONCLIENTMETRICSW { cbSize = (uint)sizeof(NONCLIENTMETRICSW) };
            PInvoke.SystemParametersInfoForDpi(SPI_GETNONCLIENTMETRICS, metrics.cbSize, &metrics, 0, dpi);
            _font = PInvoke.CreateFontIndirect(&metrics.lfMessageFont);
        }

        for (var i = 0; i < Labels.Length; i++)
        {
            Place(_labels[i], Labels[i].X, Labels[i].Y + 2, Labels[i].Width, 20, dpi);
        }

        foreach (var (id, (x, y, w, h)) in Bounds)
        {
            Place(_controls[id], x, y, w, h, dpi);
        }

        PInvoke.GetWindowRect(new HWND(_hwnd), out var window);
        PInvoke.GetClientRect(new HWND(_hwnd), out var client);
        var frameW = (window.right - window.left) - (client.right - client.left);
        var frameH = (window.bottom - window.top) - (client.bottom - client.top);
        PInvoke.SetWindowPos(new HWND(_hwnd), HWND.Null, 0, 0, Scale(ClientWidth, dpi) + frameW,
            Scale(ClientHeight, dpi) + frameH, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        if (!oldFont.IsNull && oldFont != _font)
        {
            PInvoke.DeleteObject(oldFont);
        }
    }

    private void Place(nint control, int x, int y, int width, int height, uint dpi)
    {
        PInvoke.SetWindowPos(new HWND(control), HWND.Null, Scale(x, dpi), Scale(y, dpi), Scale(width, dpi),
            Scale(height, dpi), SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        Win32Controls.SetFont(control, _font);
    }

    private void OnCommand(int id, int code)
    {
        switch (id)
        {
            case ProfileList when code == Win32Controls.LBN_SELCHANGE:
                _editor.OnListSelectionChanged();
                break;
            case SizeModeCombo when code == Win32Controls.CBN_SELCHANGE:
                _editor.OnSizeModeChanged();
                break;
            case AddButton when code == Win32Controls.BN_CLICKED:
                _editor.Add(_hwnd);
                break;
            case RemoveButton when code == Win32Controls.BN_CLICKED:
                _editor.Remove();
                break;
            case OpenConfigButton when code == Win32Controls.BN_CLICKED:
                _openConfig();
                break;
            case SaveButton when code == Win32Controls.BN_CLICKED:
                Save();
                break;
            case CancelButton when code == Win32Controls.BN_CLICKED:
                Close();
                break;
        }
    }

    private void Save()
    {
        _editor.Commit();
        var hotkey = Win32Controls.GetHotkey(_controls[HotkeyControl]);
        if (hotkey != 0)
        {
            _draft.Hotkey = SettingsDraft.HotkeyFromControl(hotkey);
        }

        _draft.Autostart = (AutostartMode)Math.Max(0, Win32Controls.GetSelection(_controls[AutostartCombo], isCombo: true));
        if (_onSave(_draft.ToConfig()).HotkeyRegistered)
        {
            Close();
            return;
        }

        Win32Controls.SetText(_controls[HotkeyWarning], "Hotkey đang bị ứng dụng khác dùng");
    }

    private void Close()
    {
        MessageWindow.UnregisterDialog(_hwnd);
        PInvoke.DestroyWindow(new HWND(_hwnd));
        if (!_font.IsNull)
        {
            PInvoke.DeleteObject(_font);
        }

        _current = null;
    }

    private static void RegisterClassOnce()
    {
        if (_classRegistered)
        {
            return;
        }

        var controls = new INITCOMMONCONTROLSEX
        {
            dwSize = (uint)sizeof(INITCOMMONCONTROLSEX),
            dwICC = INITCOMMONCONTROLSEX_ICC.ICC_HOTKEY_CLASS | INITCOMMONCONTROLSEX_ICC.ICC_STANDARD_CLASSES,
        };
        PInvoke.InitCommonControlsEx(&controls);
        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = PInvoke.GetModuleHandle((PCWSTR)null),
                hCursor = PInvoke.LoadCursor(HINSTANCE.Null, PInvoke.IDC_ARROW),
                hbrBackground = new HBRUSH(ColorBtnFace + 1),
                lpszClassName = cls,
            };
            PInvoke.RegisterClassEx(in wc);
        }

        _classRegistered = true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var window = _current;
        if (window is not null && window._hwnd == hwnd)
        {
            try
            {
                switch (msg)
                {
                    case Win32Controls.WM_COMMAND:
                        window.OnCommand((int)(wParam.Value & 0xFFFF), (int)((wParam.Value >> 16) & 0xFFFF));
                        return new LRESULT(0);
                    case WM_CLOSE:
                        window.Close();
                        return new LRESULT(0);
                    case WM_DPICHANGED:
                        var r = (RECT*)lParam.Value;
                        PInvoke.SetWindowPos(hwnd, HWND.Null, r->left, r->top, r->right - r->left, r->bottom - r->top,
                            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
                        window.Layout();
                        return new LRESULT(0);
                }
            }
            catch (Exception ex)
            {
                MessageWindow.ReportHandlerFailure(ex);
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
