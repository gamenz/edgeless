using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Edgeless.Core.Tests.Audit;

/// <summary>The antiCheat section of scripts/gate.rules.json (single source of truth, never copied).</summary>
public sealed record AntiCheatRules(
    string[] SourceInclude,
    string[] ForbiddenTokens,
    RestrictedToken[] RestrictedTokens,
    string OpenProcessFile,
    string OpenProcessRequiredRight,
    string[] OpenProcessAllowedIdentifiers,
    string NativeMethodsFile,
    string[] NativeMethodsForeignWhitelist,
    string[] NativeMethodsOwnWindowOnly,
    string[] NativeMethodsAllowedPrefixes);

public sealed record RestrictedToken(string Token, string[] AllowedIn);

/// <summary>Re-implements the gate's anti-cheat checks so they also run in dotnet test.</summary>
public static partial class InteropAudit
{
    private static readonly string[] SkippedDirs = ["bin", "obj"];

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Edgeless.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Edgeless.slnx not found above test output");
    }

    public static AntiCheatRules LoadRules(string repoRoot)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "scripts", "gate.rules.json")));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return doc.RootElement.GetProperty("antiCheat").Deserialize<AntiCheatRules>(options)
            ?? throw new InvalidOperationException("antiCheat section missing");
    }

    /// <summary>Relative '/'-separated paths of files under <paramref name="root"/>/src matching the source globs.</summary>
    public static List<string> SourceFiles(string root, AntiCheatRules rules)
    {
        var src = Path.Combine(root, "src");
        if (!Directory.Exists(src))
        {
            return [];
        }

        return Walk(src)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(rel => Matches(rel, rules.SourceInclude))
            .ToList();
    }

    public static List<string> CheckNativeMethods(string root, AntiCheatRules rules)
    {
        var path = Path.Combine(root, rules.NativeMethodsFile);
        if (!File.Exists(path))
        {
            return [];
        }

        var allowed = rules.NativeMethodsForeignWhitelist.Concat(rules.NativeMethodsOwnWindowOnly).ToHashSet(StringComparer.Ordinal);
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(name => name.Length > 0 && !name.StartsWith("//", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name)
                && !rules.NativeMethodsAllowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .Select(name => $"{rules.NativeMethodsFile}: '{name}' not allowlisted")
            .ToList();
    }

    public static List<string> CheckForbiddenTokens(string root, AntiCheatRules rules)
        => [.. from rel in SourceFiles(root, rules)
               let text = File.ReadAllText(Path.Combine(root, rel))
               from token in rules.ForbiddenTokens
               where text.Contains(token, StringComparison.Ordinal)
               select $"{rel}: forbidden '{token}'"];

    public static List<string> CheckRestrictedTokens(string root, AntiCheatRules rules)
        => [.. from rel in SourceFiles(root, rules)
               let text = File.ReadAllText(Path.Combine(root, rel))
               from restricted in rules.RestrictedTokens
               where text.Contains(restricted.Token, StringComparison.Ordinal) && !Matches(rel, restricted.AllowedIn)
               select $"{rel}: '{restricted.Token}' outside allowed files"];

    public static List<string> CheckOpenProcessRights(string root, AntiCheatRules rules)
    {
        var path = Path.Combine(root, rules.OpenProcessFile);
        if (!File.Exists(path))
        {
            return [];
        }

        return ProcessRightRegex().Matches(File.ReadAllText(path))
            .Select(m => m.Value)
            .Where(v => v != rules.OpenProcessRequiredRight && !rules.OpenProcessAllowedIdentifiers.Contains(v))
            .Select(v => $"{rules.OpenProcessFile}: process right '{v}' not allowed")
            .ToList();
    }

    private static bool Matches(string relativePath, string[] globs)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(globs);
        return matcher.Match(relativePath).HasMatches;
    }

    private static IEnumerable<string> Walk(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            yield return file;
        }

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            if (!SkippedDirs.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
            {
                foreach (var file in Walk(sub))
                {
                    yield return file;
                }
            }
        }
    }

    [GeneratedRegex("PROCESS_[A-Z_]+")]
    private static partial Regex ProcessRightRegex();
}
