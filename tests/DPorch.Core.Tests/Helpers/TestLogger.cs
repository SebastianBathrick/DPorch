using System.Text;
using DPorch.Logging;

namespace DPorch.Tests.Helpers;

/// <summary>
///     A test logger implementation that captures log entries for assertions.
/// </summary>
public class TestLogger : ILogger
{
    public List<LogEntry> Entries { get; } = [];
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Trace;

    public void Write(LogLevel lvl, string msg, params object?[]? props)
    {
        var formattedMsg = InsertPropertyValues(msg, props);
        Entries.Add(new LogEntry(lvl, formattedMsg, null));
    }

    public void Write(LogLevel lvl, string msg, Exception ex, params object?[]? props)
    {
        var formattedMsg = InsertPropertyValues(msg, props);
        Entries.Add(new LogEntry(lvl, formattedMsg, ex));
    }

    public void Info(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Info)
            return;

        Write(LogLevel.Info, msg, props);
    }

    public void Warn(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Warning)
            return;

        Write(LogLevel.Warning, msg, props);
    }

    public void Error(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Error)
            return;

        Write(LogLevel.Error, msg, props);
    }

    public void Error(Exception ex, string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Error)
            return;

        Write(LogLevel.Error, msg, ex, props);
    }

    public void Fatal(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Fatal)
            return;

        Write(LogLevel.Fatal, msg, props);
    }

    public void Fatal(Exception ex, string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Fatal)
            return;

        Write(LogLevel.Fatal, msg, ex, props);
    }

    public void Debug(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Debug)
            return;

        Write(LogLevel.Debug, msg, props);
    }

    public void Trace(string msg, params object?[]? props)
    {
        if (MinimumLogLevel > LogLevel.Trace)
            return;

        Write(LogLevel.Trace, msg, props);
    }

    public ILogger ForContext(string propertyName, object? value, bool destructureObjects = false)
    {
        return this;
    }

    public ILogger ForContext(Type source)
    {
        return this;
    }

    public ILogger ForContext<TSource>()
    {
        return this;
    }

    public void Clear()
    {
        Entries.Clear();
    }

    public bool HasEntry(LogLevel level, string messageContains)
    {
        return Entries.Any(e => e.Level == level && e.Message.Contains(messageContains));
    }

    private static string InsertPropertyValues(string msg, params object?[]? props)
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
                    sb.Append(props[propIndex++]);

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

public record LogEntry(LogLevel Level, string Message, Exception? Exception);