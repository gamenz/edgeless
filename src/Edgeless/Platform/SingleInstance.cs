using Edgeless.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Edgeless.Platform;

/// <summary>Named-mutex single instance. Must be disposed on the thread that acquired it.</summary>
internal sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\Edgeless.SingleInstance";

    private readonly Mutex _mutex;
    private bool _released;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    public static SingleInstance? TryAcquire(TimeSpan wait) => TryAcquire(MutexName, wait);

    internal static SingleInstance? TryAcquire(string name, TimeSpan wait)
    {
        var mutex = new Mutex(initiallyOwned: false, name);
        try
        {
            if (mutex.WaitOne(wait))
            {
                return new SingleInstance(mutex);
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing; we own it now.
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>Asks the running instance to open its settings window.</summary>
    public static void SignalExisting()
    {
        var target = PInvoke.FindWindow(MessageWindow.ClassName, null);
        if (!target.IsNull)
        {
            PInvoke.PostMessage(target, PInvoke.RegisterWindowMessage(MessageWindow.ShowSettingsMessage), 0, 0);
        }
    }

    public void Dispose()
    {
        if (!_released)
        {
            _released = true;
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
