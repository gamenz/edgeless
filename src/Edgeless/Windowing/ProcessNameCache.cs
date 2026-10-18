using Edgeless.Core.Rules;
using Edgeless.Interop;

namespace Edgeless.Windowing;

/// <summary>
/// Normalized exe name per PID, cached briefly so one scan does not reopen the same process for every window.
/// Entries expire after 2 seconds because Windows reuses PIDs; failures are never cached.
/// </summary>
internal sealed class ProcessNameCache(TimeProvider time, Func<uint, string?> queryImagePath)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);

    private readonly Dictionary<uint, (string Name, DateTimeOffset Expires)> _names = [];

    public ProcessNameCache()
        : this(TimeProvider.System, ForeignProcess.TryGetImagePath)
    {
    }

    public string? GetExeName(uint pid)
    {
        var now = time.GetUtcNow();
        if (_names.TryGetValue(pid, out var cached) && cached.Expires > now)
        {
            return cached.Name;
        }

        var path = queryImagePath(pid);
        if (path is null)
        {
            _names.Remove(pid);
            return null;
        }

        var name = ExeNameMatcher.Normalize(path);
        _names[pid] = (name, now + Lifetime);
        return name;
    }

    public void Forget(uint pid) => _names.Remove(pid);
}
