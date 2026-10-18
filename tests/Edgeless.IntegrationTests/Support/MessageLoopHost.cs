using Edgeless.App;
using Edgeless.Core.Config;
using Edgeless.Core.Logging;
using Edgeless.Core.Rules;
using Edgeless.Events;
using Edgeless.Shell;
using Edgeless.Windowing;

namespace Edgeless.IntegrationTests.Support;

/// <summary>Runs a real controller with its message loop on a dedicated STA thread, like the app's UI thread.</summary>
public sealed class MessageLoopHost : IDisposable
{
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private MessageWindow? _window;
    private Exception? _startupError;

    internal MessageLoopHost(AppConfig config, bool start = true)
    {
        _thread = new Thread(() => Run(config, start)) { IsBackground = true, Name = "EdgelessTestUi" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(10));
        if (_startupError is not null)
        {
            throw new InvalidOperationException("Host failed to start", _startupError);
        }
    }

    internal EdgelessController Controller { get; private set; } = null!;

    internal WindowRegistry Registry { get; private set; } = null!;

    internal FakeNotifier Notifier { get; } = new();

    internal RecordingLog Log { get; } = new();

    public T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Exception? error = null;
        using var done = new ManualResetEventSlim();
        _window!.Post(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });
        if (!done.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("UI thread did not respond");
        }

        return error is null ? result : throw new InvalidOperationException("UI call failed", error);
    }

    public void Invoke(Action action) => Invoke(() => { action(); return 0; });

    public void Dispose()
    {
        if (_window is not null)
        {
            Invoke(() =>
            {
                Controller.Dispose();
                _window.Dispose();
                MessageWindow.PostQuit();
            });
        }

        _thread.Join(TimeSpan.FromSeconds(10));
        _ready.Dispose();
    }

    private void Run(AppConfig config, bool start)
    {
        try
        {
            _window = new MessageWindow();
            Registry = new WindowRegistry();
            var finder = new WindowFinder(new ProcessPathCache(), (uint)Environment.ProcessId);
            var applier = new BorderlessApplier(Registry, Log);
            Controller = new EdgelessController(config, finder, applier, Registry, new WinEventWatcher(), _window,
                Notifier, new ReapplyGuard(TimeProvider.System), Log);
            if (start)
            {
                Controller.Start();
            }
        }
        catch (Exception ex)
        {
            _startupError = ex;
        }
        finally
        {
            _ready.Set();
        }

        if (_startupError is null)
        {
            MessageWindow.RunMessageLoop();
        }
    }
}

internal sealed class FakeNotifier : INotifier
{
    private readonly List<string> _keys = [];

    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_keys)
            {
                return [.. _keys];
            }
        }
    }

    public void Notify(string key, string title, string text)
    {
        lock (_keys)
        {
            _keys.Add(key);
        }
    }
}

internal sealed class RecordingLog : ILog
{
    private readonly List<string> _lines = [];

    public int Count(string prefix)
    {
        lock (_lines)
        {
            return _lines.Count(l => l.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    public void Info(string message) => Add(message);

    public void Warn(string message) => Add(message);

    public void Error(string message) => Add(message);

    private void Add(string message)
    {
        lock (_lines)
        {
            _lines.Add(message);
        }
    }
}
