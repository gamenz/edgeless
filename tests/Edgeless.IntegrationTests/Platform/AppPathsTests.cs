using Edgeless.Platform;

namespace Edgeless.IntegrationTests.Platform;

public sealed class AppPathsTests
{
    [Fact]
    public void ConfigAndLogs_LiveNextToTheExe()
    {
        var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath)!;

        Assert.Equal(exeDirectory, AppPaths.ExeDirectory);
        Assert.Equal(exeDirectory, AppPaths.ConfigDirectory);
        Assert.Equal(Path.Combine(exeDirectory, "logs"), AppPaths.LogDirectory);
    }

    [Fact]
    public void IsDirectoryWritable_TrueForTempAndLeavesNoFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "edgeless-paths-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.True(AppPaths.IsDirectoryWritable(dir));
            Assert.Empty(Directory.GetFileSystemEntries(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsDirectoryWritable_FalseWhenPathIsUnderAFile()
    {
        var file = Path.GetTempFileName();
        try
        {
            Assert.False(AppPaths.IsDirectoryWritable(Path.Combine(file, "sub")));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void NotWritableMessage_NamesTheFolder()
        => Assert.Contains(@"C:\Program Files\Edgeless", AppPaths.NotWritableMessage(@"C:\Program Files\Edgeless"),
            StringComparison.Ordinal);
}
