using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Shell.Settings;

/// <summary>Create/read/write helpers for the settings window's own child controls.</summary>
internal static unsafe class Win32Controls
{
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_TABSTOP = 0x00010000;
    public const uint WS_VSCROLL = 0x00200000;
    public const uint WS_HSCROLL = 0x00100000;
    public const uint WS_EX_CLIENTEDGE = 0x00000200;
    public const uint LBS_NOTIFY = 0x0001;
    public const uint LBS_NOINTEGRALHEIGHT = 0x0100;
    public const uint CBS_DROPDOWNLIST = 0x0003;
    public const uint BS_AUTOCHECKBOX = 0x0003;
    public const uint BS_DEFPUSHBUTTON = 0x0001;
    public const uint ES_AUTOHSCROLL = 0x0080;
    public const uint ES_READONLY = 0x0800;

    public const uint WM_COMMAND = 0x0111;
    public const int LBN_SELCHANGE = 1;
    public const int CBN_SELCHANGE = 1;
    public const int BN_CLICKED = 0;

    private const uint WM_SETFONT = 0x0030;
    private const uint LB_ADDSTRING = 0x0180;
    private const uint LB_RESETCONTENT = 0x0184;
    private const uint LB_SETCURSEL = 0x0186;
    private const uint LB_GETCURSEL = 0x0188;
    private const uint LB_SETHORIZONTALEXTENT = 0x0194;
    private const uint CB_ADDSTRING = 0x0143;
    private const uint CB_GETCURSEL = 0x0147;
    private const uint CB_RESETCONTENT = 0x014B;
    private const uint CB_SETCURSEL = 0x014E;
    private const uint BM_GETCHECK = 0x00F0;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint HKM_SETHOTKEY = 0x0401;
    private const uint HKM_GETHOTKEY = 0x0402;

    public static nint Create(nint parent, string className, string text, uint style, int id, uint exStyle = 0)
    {
        fixed (char* cls = className)
        fixed (char* caption = text)
        {
            return PInvoke.CreateWindowEx((WINDOW_EX_STYLE)exStyle, cls, caption,
                (WINDOW_STYLE)(WS_CHILD | WS_VISIBLE | style), 0, 0, 0, 0, new HWND(parent), new HMENU(id),
                PInvoke.GetModuleHandle((PCWSTR)null), null);
        }
    }

    public static void SetFont(nint control, nint font) => Send(control, WM_SETFONT, (nuint)font, 1);

    public static void SetText(nint control, string text) => PInvoke.SetWindowText(new HWND(control), text);

    public static string GetText(nint control) => Interop.ForeignWindow.GetTitle(control);

    public static void Enable(nint control, bool enabled) => PInvoke.EnableWindow(new HWND(control), enabled);

    public static bool GetCheck(nint checkbox) => Send(checkbox, BM_GETCHECK, 0, 0) == 1;

    public static void SetCheck(nint checkbox, bool value) => Send(checkbox, BM_SETCHECK, value ? 1u : 0u, 0);

    public static void SetItems(nint control, bool isCombo, IEnumerable<string> items)
    {
        Send(control, isCombo ? CB_RESETCONTENT : LB_RESETCONTENT, 0, 0);
        foreach (var item in items)
        {
            fixed (char* text = item)
            {
                Send(control, isCombo ? CB_ADDSTRING : LB_ADDSTRING, 0, (nint)text);
            }
        }
    }

    public static int GetSelection(nint control, bool isCombo) => (int)Send(control, isCombo ? CB_GETCURSEL : LB_GETCURSEL, 0, 0);

    public static void SetSelection(nint control, bool isCombo, int index)
        => Send(control, isCombo ? CB_SETCURSEL : LB_SETCURSEL, unchecked((nuint)index), 0);

    public static ushort GetHotkey(nint control) => (ushort)Send(control, HKM_GETHOTKEY, 0, 0);

    public static void SetHotkey(nint control, ushort value) => Send(control, HKM_SETHOTKEY, value, 0);

    /// <summary>Scrollable width of a listbox with WS_HSCROLL, in pixels.</summary>
    public static void SetHorizontalExtent(nint listBox, int pixels)
        => Send(listBox, LB_SETHORIZONTALEXTENT, (nuint)Math.Max(0, pixels), 0);

    private static nint Send(nint control, uint message, nuint wParam, nint lParam)
        => PInvoke.SendMessage(new HWND(control), message, wParam, lParam);
}
