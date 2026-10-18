namespace Edgeless.Core.Rules;

/// <summary>Shows the same notification key at most once per interval (default 5 minutes).</summary>
public sealed class NotificationThrottle(TimeProvider time, TimeSpan? interval = null)
{
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, DateTimeOffset> _lastShown = new(StringComparer.Ordinal);

    public bool ShouldShow(string key)
    {
        var now = time.GetUtcNow();
        if (_lastShown.TryGetValue(key, out var last) && now - last < _interval)
        {
            return false;
        }

        _lastShown[key] = now;
        return true;
    }
}
