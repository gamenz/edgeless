using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;

namespace Edgeless.Interop;

/// <summary>Friendly monitor names (e.g. "DELL U2720Q") via the display-config API. System query, no foreign handle.</summary>
internal static unsafe class DisplayNames
{
    /// <summary>GDI device name (\\.\DISPLAY1) to friendly monitor name; empty when the query fails.</summary>
    public static IReadOnlyDictionary<string, string> Query()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ActivePaths())
        {
            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            source.header.adapterId = path.sourceInfo.adapterId;
            source.header.id = path.sourceInfo.id;

            var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
            target.header.adapterId = path.targetInfo.adapterId;
            target.header.id = path.targetInfo.id;

            if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0 || PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0)
            {
                continue;
            }

            var friendly = target.monitorFriendlyDeviceName.ToString();
            if (friendly.Length > 0)
            {
                names.TryAdd(source.viewGdiDeviceName.ToString(), friendly);
            }
        }

        return names;
    }

    private static DISPLAYCONFIG_PATH_INFO[] ActivePaths()
    {
        const QUERY_DISPLAY_CONFIG_FLAGS flags = QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint pathCount, modeCount;
            if (PInvoke.GetDisplayConfigBufferSizes(flags, &pathCount, &modeCount) != WIN32_ERROR.ERROR_SUCCESS)
            {
                return [];
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            WIN32_ERROR result;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                result = PInvoke.QueryDisplayConfig(flags, &pathCount, p, &modeCount, m, null);
            }

            if (result == WIN32_ERROR.ERROR_SUCCESS)
            {
                return paths[..(int)pathCount];
            }

            // The topology changed between the two calls: ask for fresh sizes.
            if (result != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
            {
                return [];
            }
        }

        return [];
    }
}
