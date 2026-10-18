using Edgeless.Core.Config;
using Edgeless.Core.Rules;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Edgeless.Platform;

/// <summary>
/// Resolves subst drives, junctions and symlinks to the file's real path, so a profile picked through an alias still
/// matches the path Windows reports for the running process. Opens the file for attributes only (like Explorer);
/// any failure returns the normalized input unchanged.
/// </summary>
internal static unsafe class PathCanonicalizer
{
    private const uint FileReadAttributes = 0x0080;
    private const int MaxPath = 32768;

    public static string Canonicalize(string path)
    {
        var normalized = ExePath.Normalize(path);
        if (normalized.Length == 0)
        {
            return path;
        }

        HANDLE handle;
        fixed (char* name = normalized)
        {
            handle = PInvoke.CreateFile(name, FileReadAttributes,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null, FILE_CREATION_DISPOSITION.OPEN_EXISTING, FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS,
                HANDLE.Null);
        }

        if (handle.IsNull || (nint)handle.Value == -1) // INVALID_HANDLE_VALUE
        {
            return normalized;
        }

        try
        {
            var buffer = new char[MaxPath];
            uint length;
            fixed (char* ptr = buffer)
            {
                length = PInvoke.GetFinalPathNameByHandle(handle, ptr, MaxPath, 0); // FILE_NAME_NORMALIZED | VOLUME_NAME_DOS
            }

            return length == 0 || length >= MaxPath ? normalized : StripPrefix(new string(buffer, 0, (int)length));
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }

    /// <summary>Canonicalizes every profile path (hand-edited configs may use aliases), then drops new duplicates.</summary>
    public static AppConfig CanonicalizeProfiles(AppConfig config)
    {
        foreach (var profile in config.Profiles)
        {
            profile.Path = Canonicalize(profile.Path);
        }

        return ConfigSerializer.Normalize(config);
    }

    private static string StripPrefix(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[8..];
        }

        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }
}
