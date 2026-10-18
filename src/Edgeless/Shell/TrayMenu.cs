using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Shell;

/// <summary>Renders <see cref="TrayMenuModel"/> as a Win32 popup menu and reports the chosen command.</summary>
internal sealed unsafe class TrayMenu(nint ownerHwnd)
{
    private const uint FirstId = 1;

    public void Show(TrayMenuState state, int x, int y, Action<TrayCommand, nint> onCommand)
    {
        var commands = new List<(TrayCommand Command, nint Hwnd)>();
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            Append(menu, TrayMenuModel.Build(state), commands);

            // Required so the menu closes when the user clicks elsewhere (own window only).
            PInvoke.SetForegroundWindow(new HWND(ownerHwnd));
            var chosen = PInvoke.TrackPopupMenu(menu,
                TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY,
                x, y, 0, new HWND(ownerHwnd), null);
            PInvoke.PostMessage(new HWND(ownerHwnd), 0, 0, 0); // WM_NULL, per TrackPopupMenu docs

            var index = (int)chosen.Value - (int)FirstId;
            if (index >= 0 && index < commands.Count)
            {
                onCommand(commands[index].Command, commands[index].Hwnd);
            }
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }

    private static void Append(HMENU menu, IReadOnlyList<TrayMenuItem> items, List<(TrayCommand, nint)> commands)
    {
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                AppendItem(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, "");
                continue;
            }

            var flags = MENU_ITEM_FLAGS.MF_STRING
                | (item.Enabled ? 0 : MENU_ITEM_FLAGS.MF_GRAYED)
                | (item.Checked ? MENU_ITEM_FLAGS.MF_CHECKED : 0);
            if (item.Children.Count > 0 || item.Command is null)
            {
                var sub = PInvoke.CreatePopupMenu();
                Append(sub, item.Children, commands);
                AppendItem(menu, flags | MENU_ITEM_FLAGS.MF_POPUP, (nuint)sub.Value, item.Text);
                continue;
            }

            commands.Add((item.Command.Value, item.Hwnd));
            AppendItem(menu, flags, (nuint)(FirstId + commands.Count - 1), item.Text);
        }
    }

    private static void AppendItem(HMENU menu, MENU_ITEM_FLAGS flags, nuint id, string text)
    {
        fixed (char* ptr = text)
        {
            PInvoke.AppendMenu(menu, flags, id, ptr);
        }
    }
}
