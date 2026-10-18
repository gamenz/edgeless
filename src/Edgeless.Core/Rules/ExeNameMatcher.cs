namespace Edgeless.Core.Rules;

public static class ExeNameMatcher
{
    /// <summary>File name only, trimmed, lower-case, with ".exe" appended when missing; "" for blank input.</summary>
    public static string Normalize(string exeOrPath)
    {
        var trimmed = exeOrPath.Trim();
        var slash = trimmed.LastIndexOfAny(['\\', '/']);
        var name = (slash >= 0 ? trimmed[(slash + 1)..] : trimmed).Trim().ToLowerInvariant();
        if (name.Length == 0)
        {
            return "";
        }

        return name.EndsWith(".exe", StringComparison.Ordinal) ? name : name + ".exe";
    }
}
