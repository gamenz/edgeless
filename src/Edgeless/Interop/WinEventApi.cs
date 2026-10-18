using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Edgeless.Interop;

/// <summary>WinEvent hooks — always out-of-context (no code ever runs inside another process).</summary>
internal static unsafe class WinEventApi
{
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;

    /// <param name="processId">0 = all processes.</param>
    /// <returns>Hook handle, or 0 on failure.</returns>
    public static nint Hook(
        uint eventMin, uint eventMax, uint processId,
        delegate* unmanaged[Stdcall]<nint, uint, nint, int, int, uint, uint, void> callback)
    {
        var proc = (delegate* unmanaged[Stdcall]<HWINEVENTHOOK, uint, HWND, int, int, uint, uint, void>)callback;
        var hook = PInvoke.SetWinEventHook(
            eventMin, eventMax, HMODULE.Null, proc, processId, 0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
        return (nint)hook.Value;
    }

    public static void Unhook(nint hook)
    {
        if (hook != 0)
        {
            PInvoke.UnhookWinEvent(new HWINEVENTHOOK(hook));
        }
    }
}
