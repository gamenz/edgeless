using System.Text;

namespace Edgeless.Core.Logging;

/// <summary>
/// Thread-safe rolling file logger: edgeless.log, rolled to edgeless.1.log, edgeless.2.log ... before a write
/// would exceed <c>maxBytes</c>. Flushes every line and never throws.
/// </summary>
public sealed class FileLogger : ILog, IDisposable
{
    private const string BaseName = "edgeless";

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly Lock _lock = new();
    private FileStream? _stream;

    public FileLogger(string directory, TimeProvider time, long maxBytes = 1_048_576, int maxFiles = 3)
    {
        _directory = directory;
        _time = time;
        _maxBytes = maxBytes;
        _maxFiles = Math.Max(1, maxFiles);
    }

    public string CurrentPath => FilePath(0);

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    public void Dispose()
    {
        lock (_lock)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    private void Write(string level, string message)
    {
        var line = $"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}\n";
        var bytes = Encoding.UTF8.GetBytes(line);
        lock (_lock)
        {
            try
            {
                var stream = _stream ??= Open();
                if (stream.Length > 0 && stream.Length + bytes.Length > _maxBytes)
                {
                    stream = Roll();
                }

                stream.Write(bytes);
                stream.Flush();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private FileStream Open()
    {
        Directory.CreateDirectory(_directory);
        return new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    private FileStream Roll()
    {
        _stream?.Dispose();
        _stream = null;
        File.Delete(FilePath(_maxFiles - 1));
        for (var index = _maxFiles - 2; index >= 0; index--)
        {
            if (File.Exists(FilePath(index)))
            {
                File.Move(FilePath(index), FilePath(index + 1));
            }
        }

        return _stream = Open();
    }

    private string FilePath(int index)
        => Path.Combine(_directory, index == 0 ? $"{BaseName}.log" : $"{BaseName}.{index}.log");
}
