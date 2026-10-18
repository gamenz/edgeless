using Edgeless.Core.Rules;
using Edgeless.Interop;
using Edgeless.Platform;

namespace Edgeless.Windowing;

/// <summary>
/// Normalized image path per PID, cached briefly so one scan does not reopen the same process for every window.
/// Entries expire after 2 seconds because Windows reuses PIDs; failures are never cached.
/// </summary>
internal sealed class ProcessPathCache(TimeProvider time, Func<uint, string?> queryImagePath)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);

    private readonly Dictionary<uint, (string Path, DateTimeOffset Expires)> _paths = [];

    public ProcessPathCache()
        : this(TimeProvider.System, QueryCanonicalImagePath)
    {
    }

    public string? GetImagePath(uint pid)
    {
        var now = time.GetUtcNow();
        if (_paths.TryGetValue(pid, out var cached) && cached.Expires > now)
        {
            return cached.Path;
        }

        var path = ExePath.Normalize(queryImagePath(pid) ?? "");
        if (path.Length == 0)
        {
            _paths.Remove(pid);
            return null;
        }

        _paths[pid] = (path, now + Lifetime);
        return path;
    }

    public void Forget(uint pid) => _paths.Remove(pid);

    /// <summary>Process image path resolved to its real location (same form as profile paths).</summary>
    private static string? QueryCanonicalImagePath(uint pid)
        => ForeignProcess.TryGetImagePath(pid) is { } path ? PathCanonicalizer.Canonicalize(path) : null;
}
