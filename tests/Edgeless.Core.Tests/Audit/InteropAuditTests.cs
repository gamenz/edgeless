namespace Edgeless.Core.Tests.Audit;

/// <summary>Anti-cheat audit (CLAUDE.md section 1) over the real source tree.</summary>
public sealed class InteropAuditTests
{
    private static readonly string Root = InteropAudit.FindRepoRoot();
    private static readonly AntiCheatRules Rules = InteropAudit.LoadRules(Root);

    [Fact]
    public void NativeMethodsTxt_OnlyAllowlistedApis()
        => Assert.Empty(InteropAudit.CheckNativeMethods(Root, Rules));

    [Fact]
    public void Source_ContainsNoForbiddenTokens()
        => Assert.Empty(InteropAudit.CheckForbiddenTokens(Root, Rules));

    [Fact]
    public void RestrictedTokens_OnlyInAllowedFiles()
        => Assert.Empty(InteropAudit.CheckRestrictedTokens(Root, Rules));

    [Fact]
    public void OpenProcessFile_UsesOnlyLimitedRight()
        => Assert.Empty(InteropAudit.CheckOpenProcessRights(Root, Rules));

    [Fact]
    public void Rules_AllowOnlyTheProcessRightTypeNameBesidesLimitedRight()
        => Assert.Equal(["PROCESS_ACCESS_RIGHTS"], Rules.OpenProcessAllowedIdentifiers);

    [Fact]
    public void Audit_ScansRealSources()
        => Assert.Contains(InteropAudit.SourceFiles(Root, Rules), f => f.EndsWith("Program.cs", StringComparison.Ordinal));

    [Fact]
    public void Audit_DetectsViolations()
    {
        using var fixture = new TempDirectory();
        Write(fixture.Path, "src/Edgeless/Bad.cs", "var m = p.MainModule; SetWindowLongPtr(h, i, v);");
        Write(fixture.Path, "src/Edgeless/Interop/ForeignProcess.cs", "OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION)");
        Write(fixture.Path, Rules.NativeMethodsFile, "GetForegroundWindow\nCreateRemoteThread\nWS_POPUP\n");

        Assert.Contains(InteropAudit.CheckForbiddenTokens(fixture.Path, Rules),
            v => v.Contains(".MainModule", StringComparison.Ordinal));
        Assert.Contains(InteropAudit.CheckRestrictedTokens(fixture.Path, Rules),
            v => v.Contains("SetWindowLongPtr", StringComparison.Ordinal));
        Assert.Single(InteropAudit.CheckOpenProcessRights(fixture.Path, Rules));
        Assert.Single(InteropAudit.CheckNativeMethods(fixture.Path, Rules));
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
