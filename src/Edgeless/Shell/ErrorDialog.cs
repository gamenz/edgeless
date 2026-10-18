using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Edgeless.Shell;

/// <summary>Ownerless error message box for failures before the tray icon exists.</summary>
internal static unsafe class ErrorDialog
{
    public static void Show(string text)
    {
        fixed (char* message = text)
        fixed (char* caption = "Edgeless")
        {
            PInvoke.MessageBox(HWND.Null, message, caption, MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);
        }
    }
}
