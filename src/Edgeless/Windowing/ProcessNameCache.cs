using Edgeless.Core.Rules;
using Edgeless.Interop;

namespace Edgeless.Windowing;

/// <summary>Normalized exe name per PID. Only successes are cached, so a reused PID is re-queried after Forget.</summary>
internal sealed class ProcessNameCache
{
    private readonly Dictionary<uint, string> _names = [];

    public string? GetExeName(uint pid)
    {
        if (_names.TryGetValue(pid, out var cached))
        {
            return cached;
        }

        var path = ForeignProcess.TryGetImagePath(pid);
        if (path is null)
        {
            return null;
        }

        var name = ExeNameMatcher.Normalize(path);
        _names[pid] = name;
        return name;
    }

    public void Forget(uint pid) => _names.Remove(pid);
}
