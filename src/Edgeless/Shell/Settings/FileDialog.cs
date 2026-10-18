using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls.Dialogs;

namespace Edgeless.Shell.Settings;

/// <summary>The standard Open File dialog (own window only), used to pick a game exe.</summary>
internal static unsafe class FileDialog
{
    private const int MaxPath = 32768;
    private const string Filter = "Programs (*.exe)\0*.exe\0\0";
    private const string Title = "Choose the game's exe";

    /// <returns>The chosen full path, or null when the user cancels.</returns>
    public static string? PickExe(nint owner, string? initialPath)
    {
        var buffer = new char[MaxPath];
        var initialDir = string.IsNullOrEmpty(initialPath) ? null : Path.GetDirectoryName(initialPath);
        fixed (char* file = buffer)
        fixed (char* filter = Filter)
        fixed (char* title = Title)
        fixed (char* dir = initialDir)
        {
            var ofn = new OPENFILENAMEW
            {
                lStructSize = (uint)sizeof(OPENFILENAMEW),
                hwndOwner = new HWND(owner),
                lpstrFilter = filter,
                lpstrFile = file,
                nMaxFile = MaxPath,
                lpstrTitle = title,
                lpstrInitialDir = dir,
                Flags = OPEN_FILENAME_FLAGS.OFN_EXPLORER | OPEN_FILENAME_FLAGS.OFN_FILEMUSTEXIST
                    | OPEN_FILENAME_FLAGS.OFN_PATHMUSTEXIST | OPEN_FILENAME_FLAGS.OFN_NOCHANGEDIR
                    | OPEN_FILENAME_FLAGS.OFN_HIDEREADONLY,
            };
            if (!PInvoke.GetOpenFileName(&ofn))
            {
                return null;
            }
        }

        var length = Array.IndexOf(buffer, '\0');
        return new string(buffer, 0, length < 0 ? buffer.Length : length);
    }
}
