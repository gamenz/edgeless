using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Edgeless.Events;

/// <summary>The single global hotkey, registered on the app's own hidden window (no keyboard hooks).</summary>
internal sealed class HotkeyManager(nint ownerHwnd) : IDisposable
{
    private const int HotkeyId = 1;
    private bool _registered;

    /// <summary>Replaces any previous registration. Returns false when another app owns the combination.</summary>
    public bool Register(uint modifiers, uint virtualKey)
    {
        Unregister();
        _registered = PInvoke.RegisterHotKey(new HWND(ownerHwnd), HotkeyId, (HOT_KEY_MODIFIERS)modifiers, virtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            PInvoke.UnregisterHotKey(new HWND(ownerHwnd), HotkeyId);
            _registered = false;
        }
    }

    public void Dispose() => Unregister();
}
