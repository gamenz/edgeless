using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Edgeless.Interop;

/// <summary>The only place that opens a foreign process, and only with PROCESS_QUERY_LIMITED_INFORMATION.</summary>
internal static unsafe class ForeignProcess
{
    private const int MaxPath = 32768;

    /// <summary>Full image path of <paramref name="pid"/>, or null on any failure (never retried with more rights).</summary>
    public static string? TryGetImagePath(uint pid)
    {
        var handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle.IsNull)
        {
            return null;
        }

        try
        {
            var buffer = new char[MaxPath];
            var size = (uint)buffer.Length;
            fixed (char* ptr = buffer)
            {
                // Format 0 = Win32 path. Spelled as a literal so this file names exactly one access right.
                if (!PInvoke.QueryFullProcessImageName(handle, 0, new PWSTR(ptr), &size))
                {
                    return null;
                }
            }

            return new string(buffer, 0, (int)size);
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }
}
