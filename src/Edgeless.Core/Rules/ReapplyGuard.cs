namespace Edgeless.Core.Rules;

/// <summary>
/// Detects a game "fighting" Edgeless: too many real re-applies of one window inside a sliding time window.
/// </summary>
public sealed class ReapplyGuard(TimeProvider time, int maxReapplies = 5, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(10);
    private readonly Dictionary<nint, Queue<DateTimeOffset>> _history = [];

    /// <summary>Records one real re-apply. Returns false when it exceeds the limit; the caller must stop managing.</summary>
    public bool TryRecord(nint hwnd)
    {
        var now = time.GetUtcNow();
        if (!_history.TryGetValue(hwnd, out var records))
        {
            records = new Queue<DateTimeOffset>();
            _history[hwnd] = records;
        }

        while (records.Count > 0 && now - records.Peek() >= _window)
        {
            records.Dequeue();
        }

        if (records.Count >= maxReapplies)
        {
            return false;
        }

        records.Enqueue(now);
        return true;
    }

    public void Reset(nint hwnd) => _history.Remove(hwnd);
}
