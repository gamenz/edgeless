namespace Edgeless.Core.Rules;

/// <summary>Windows Edgeless must never touch, even via the hotkey.</summary>
public static class ExclusionRules
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    private const string DesktopClass = "#32769";

    /// <param name="exeName">Normalized exe name (see <see cref="ExeNameMatcher.Normalize"/>).</param>
    public static bool IsExcluded(string exeName, string className, bool isOwnProcess)
    {
        if (isOwnProcess || className == DesktopClass)
        {
            return true;
        }

        return exeName switch
        {
            "applicationframehost.exe" => true,
            "explorer.exe" => ShellClasses.Contains(className),
            _ => false,
        };
    }
}
