using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Shell;

/// <summary>The notification-area icon (Shell_NotifyIcon, NOTIFYICON_VERSION_4).</summary>
internal sealed unsafe class TrayIcon(nint ownerHwnd) : IDisposable
{
    private const uint IconId = 1;
    private const ushort AppIconResourceId = 32512; // icon group the SDK embeds from ApplicationIcon

    private HICON _icon;
    private string _tooltip = "Edgeless";
    private bool _added;

    public void Add()
    {
        _icon = LoadAppIcon();
        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON
            | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        data.uCallbackMessage = MessageWindow.WM_TRAY;
        data.hIcon = _icon;
        data.szTip = _tooltip;
        _added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, &data);
    }

    /// <summary>Re-adds the icon after Explorer restarts (TaskbarCreated).</summary>
    public void Readd()
    {
        _added = false;
        Add();
    }

    public void SetTooltip(string text)
    {
        _tooltip = text;
        if (!_added)
        {
            return;
        }

        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        data.szTip = text;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
    }

    public void ShowBalloon(string title, string text)
    {
        var data = NewData(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        data.szInfoTitle = title;
        data.szInfo = text;
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = NewData(0);
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, &data);
            _added = false;
        }
    }

    private NOTIFYICONDATAW NewData(NOTIFY_ICON_DATA_FLAGS flags)
        => new()
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = new HWND(ownerHwnd),
            uID = IconId,
            uFlags = flags,
        };

    private static HICON LoadAppIcon()
    {
        var module = PInvoke.GetModuleHandle((PCWSTR)null);
        var handle = PInvoke.LoadImage(module, (PCWSTR)(char*)AppIconResourceId, GDI_IMAGE_TYPE.IMAGE_ICON, 0, 0,
            IMAGE_FLAGS.LR_DEFAULTSIZE | IMAGE_FLAGS.LR_SHARED);
        if (handle.IsNull)
        {
            // Fallback: the stock application icon.
            handle = PInvoke.LoadImage(HINSTANCE.Null, (PCWSTR)(char*)AppIconResourceId, GDI_IMAGE_TYPE.IMAGE_ICON, 0, 0,
                IMAGE_FLAGS.LR_DEFAULTSIZE | IMAGE_FLAGS.LR_SHARED);
        }

        return new HICON(handle.Value);
    }
}
