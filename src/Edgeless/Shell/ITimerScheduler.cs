namespace Edgeless.Shell;

/// <summary>One-shot timers on the UI thread. Scheduling an existing key restarts it (debounce).</summary>
internal interface ITimerScheduler
{
    void Schedule(string key, TimeSpan delay, Action callback);

    void Cancel(string key);
}
