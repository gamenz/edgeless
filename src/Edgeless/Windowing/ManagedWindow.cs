using Edgeless.Core.Config;
using Edgeless.Core.Styles;
using Edgeless.Interop;

namespace Edgeless.Windowing;

/// <summary>A window Edgeless changed, with everything needed to put it back.</summary>
internal sealed class ManagedWindow
{
    public required nint Hwnd { get; init; }

    public required uint Pid { get; init; }

    public required string ExeName { get; init; }

    public required WindowStyles OriginalStyles { get; init; }

    public required WindowPlacementSnapshot OriginalPlacement { get; init; }

    public required GameProfile Profile { get; set; }
}
