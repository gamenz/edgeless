namespace Edgeless.Core.Rules;

/// <summary>Full exe paths: profiles match games by normalized path, because exe names can collide.</summary>
public static class ExePath
{
    /// <summary>Trimmed, '/' → '\', resolved with GetFullPath; "" when blank or not fully qualified.</summary>
    public static string Normalize(string path)
    {
        var trimmed = (path ?? "").Trim().Replace('/', '\\');
        if (trimmed.Length == 0 || !Path.IsPathFullyQualified(trimmed))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "";
        }
    }

    /// <summary>True when both are valid paths to the same file, ignoring case and separator style.</summary>
    public static bool AreSame(string a, string b)
    {
        var left = Normalize(a);
        return left.Length > 0 && string.Equals(left, Normalize(b), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Lower-case file name, for display, logs and exclusion rules only.</summary>
    public static string FileName(string path)
        => string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path.Trim().Replace('/', '\\')).ToLowerInvariant();
}
