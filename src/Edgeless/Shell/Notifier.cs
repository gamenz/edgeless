using Edgeless.Core.Logging;
using Edgeless.Core.Rules;

namespace Edgeless.Shell;

/// <summary>Tray balloon notifications (toasts on Windows 11), throttled per key.</summary>
internal sealed class Notifier(TrayIcon tray, NotificationThrottle throttle, ILog log) : INotifier
{
    public void Notify(string key, string title, string text)
    {
        log.Info($"Notify [{key}]: {text}");
        if (throttle.ShouldShow(key))
        {
            tray.ShowBalloon(title, text);
        }
    }
}
