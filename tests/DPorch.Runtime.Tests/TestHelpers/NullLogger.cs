using DPorch.Logging;

namespace DPorch.Runtime.Tests.TestHelpers;

/// <summary>
///     Logger implementation that discards all log entries.  Used for testing.
/// </summary>
public class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    private NullLogger()
    {
    }

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Fatal;

    public void Write(LogLevel lvl, string msg, params object?[]? props)
    {
        // Discard
    }

    public void Write(LogLevel lvl, string msg, Exception ex, params object?[]? props)
    {
        // Discard
    }

    public void Info(string msg, params object?[]? props)
    {
        // Discard
    }

    public void Warn(string msg, params object?[]? props)
    {
        // Discard
    }

    public void Error(string msg, params object?[]? props)
    {
        // Discard
    }

    public void Error(Exception ex, string msg, params object?[]? props)
    {
        // Discard
    }

    public void Fatal(string msg, params object?[]? props)
    {
        // Discard
    }

    public void Fatal(Exception ex, string msg, params object?[]? props)
    {
        // Discard
    }

    public void Debug(string msg, params object?[]? props)
    {
        // Discard
    }

    public void Trace(string msg, params object?[]? props)
    {
        // Discard
    }
}
