using System.Diagnostics;
using Edgeless.Platform;

namespace Edgeless.IntegrationTests.Platform;

public sealed class PathCanonicalizerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "edgeless-canon-" + Guid.NewGuid().ToString("N"));

    public PathCanonicalizerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            // Remove the junction first so the delete does not walk into its target twice.
            var link = Path.Combine(_root, "link");
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Canonicalize_ResolvesJunctionToRealPath()
    {
        var real = Directory.CreateDirectory(Path.Combine(_root, "real", "Game")).FullName;
        var exe = Path.Combine(real, "game.exe");
        File.WriteAllText(exe, "");
        var link = Path.Combine(_root, "link");
        using (var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{Path.Combine(_root, "real")}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!)
        {
            mklink.WaitForExit();
            Assert.Equal(0, mklink.ExitCode);
        }

        var viaJunction = Path.Combine(link, "Game", "game.exe");

        Assert.Equal(exe, PathCanonicalizer.Canonicalize(viaJunction), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Canonicalize_PlainPath_IsUnchangedExceptCase()
    {
        var exe = Path.Combine(_root, "Plain.exe");
        File.WriteAllText(exe, "");

        Assert.Equal(exe, PathCanonicalizer.Canonicalize(exe.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(exe, PathCanonicalizer.Canonicalize(exe)); // real casing comes back
    }

    [Fact]
    public void Canonicalize_MissingFile_ReturnsInput()
    {
        var missing = Path.Combine(_root, "nope", "game.exe");

        Assert.Equal(missing, PathCanonicalizer.Canonicalize(missing));
    }
}
