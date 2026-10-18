namespace Edgeless.Shell;

/// <summary>User-facing notifications; <paramref name="key"/> identifies a message for throttling.</summary>
internal interface INotifier
{
    void Notify(string key, string title, string text);
}
