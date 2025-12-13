using System.Text;
using DPorch.Logging;

namespace DPorch.Classes.Logging;

/// <summary>
///     Provides a thread-safe console logger implementation that writes formatted log entries to standard output.
/// </summary>
/// <remarks>
///     Uses a lock to ensure the thread-safe console writes. Supports structured logging with message template
///     placeholders.
/// </remarks>
public sealed class ConsoleLogger : ILogger
{
    const string LogMessageTemplate = "[{0}]: {1}";
    const string AnsiCyan = "\x1b[36m";
    const string AnsiReset = "\x1b[0m";
    static readonly object ConsoleLock = new();

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;

    /// <inheritdoc/>
    public void Write(LogLevel lvl, string msg, params object?[]? props)
    {
        if (lvl < MinimumLogLevel)
            return;

        var formattedMsg = InsertPropertyValues(msg, props);
        var logEntry = string.Format(LogMessageTemplate, lvl, formattedMsg);

        lock (ConsoleLock)
        {
            Console.WriteLine(logEntry);
        }
    }

    /// <inheritdoc/>
    public void Write(LogLevel lvl, string msg, Exception ex, params object?[]? props)
    {
        if (lvl < MinimumLogLevel)
            return;

        var formattedMsg = InsertPropertyValues(msg, props);
        var logEntry = string.Format(LogMessageTemplate, lvl, formattedMsg);

        lock (ConsoleLock)
        {
            Console.WriteLine(logEntry);
            Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <inheritdoc/>
    public void Info(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Info)
            return;

        Write(LogLevel.Info, msg, props);
    }

    /// <inheritdoc/>
    public void Warn(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Warning)
            return;

        Write(LogLevel.Warning, msg, props);
    }

    /// <inheritdoc/>
    public void Error(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Error)
            return;

        Write(LogLevel.Error, msg, props);
    }

    /// <inheritdoc/>
    public void Error(Exception ex, string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Error)
            return;

        Write(LogLevel.Error, msg, ex, props);
    }

    /// <inheritdoc/>
    public void Fatal(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Fatal)
            return;

        Write(LogLevel.Fatal, msg, props);
    }

    /// <inheritdoc/>
    public void Fatal(Exception ex, string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Fatal)
            return;

        Write(LogLevel.Fatal, msg, ex, props);
    }

    /// <inheritdoc/>
    public void Debug(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Debug)
            return;

        Write(LogLevel.Debug, msg, props);
    }

    /// <inheritdoc/>
    public void Trace(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Trace)
            return;

        Write(LogLevel.Trace, msg, props);
    }

    static string InsertPropertyValues(string msg, params object?[]? props)
    {
        if (props == null || props.Length == 0)
            return msg;

        var sb = new StringBuilder();
        var openDelimIndex = -1;
        var propIndex = 0;

        for (var i = 0; i < msg.Length; i++)
        {
            if (openDelimIndex != -1)
            {
                if (msg[i] != '}')
                    continue;

                if (propIndex < props.Length)
                {
                    sb.Append(AnsiCyan);
                    sb.Append(props[propIndex++]);
                    sb.Append(AnsiReset);
                }

                openDelimIndex = -1;

                continue;
            }

            if (msg[i] == '{')
                openDelimIndex = i;
            else
                sb.Append(msg[i]);
        }

        if (openDelimIndex != -1)
            sb.Append(msg.Substring(openDelimIndex));

        return sb.ToString();
    }
}