namespace DPorch.Logging;

/// <summary>
///     Provides thread-safe logging functionality with support for structured logging.
/// </summary>
/// <remarks>
///     <see cref="ILogger" /> implementations provide thread-safe logging with support for different severity levels
///     defined by <see cref="LogLevel" />. The <see cref="MinimumLogLevel" /> property filters log entries, with
///     entries below the threshold being discarded.
/// </remarks>
public interface ILogger
{
    /// <summary>
    ///     Minimum log level future instances of ILogger will initialize to.
    /// </summary>
    public static LogLevel DefaultMinimumLogLevel { get; set; } = LogLevel.Info;

    /// <summary>
    ///     Gets or sets the minimum log level that will be written.
    /// </summary>
    /// <remarks>
    ///     Log entries with a level below this threshold are discarded without being written. For example, if set to
    ///     <see cref="LogLevel.Warning" />, only Warning, Error, and Fatal entries are written. Trace, Debug, and Info
    ///     entries are ignored.
    /// </remarks>
    LogLevel MinimumLogLevel { get; set; }

    /// <summary>
    ///     Writes a log entry at the specified level with optional structured properties.
    /// </summary>
    /// <param name="lvl"> The severity level of this log entry. </param>
    /// <param name="msg">
    ///     The message template with placeholders in the format "{PropertyName}".
    /// </param>
    /// <param name="props">
    ///     Property values to substitute into the message template placeholders, in order of appearance.
    /// </param>
    /// <remarks>
    ///     If <paramref name="lvl" /> is below <see cref="MinimumLogLevel" />, the entry is discarded without being
    ///     written.
    /// </remarks>
    void Write(LogLevel lvl, string msg, params object?[]? props);

    /// <summary>
    ///     Writes a log entry at the specified level with an exception and optional structured properties.
    /// </summary>
    /// <param name="lvl"> The severity level of this log entry. </param>
    /// <param name="msg">
    ///     The message template with placeholders in the format "{PropertyName}".
    /// </param>
    /// <param name="ex"> The exception to include with this log entry. </param>
    /// <param name="props">
    ///     Property values to substitute into the message template placeholders, in order of appearance.
    /// </param>
    /// <remarks>
    ///     If <paramref name="lvl" /> is below <see cref="MinimumLogLevel" />, the entry is discarded without being
    ///     written.
    /// </remarks>
    void Write(LogLevel lvl, string msg, Exception ex, params object?[]? props);

    /// <summary>
    ///     Writes an informational log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Info(string msg, params object?[]? props);

    /// <summary>
    ///     Writes a warning log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Warn(string msg, params object?[]? props);

    /// <summary>
    ///     Writes an error log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Error(string msg, params object?[]? props);

    /// <summary>
    ///     Writes an error log entry with an exception.
    /// </summary>
    /// <param name="ex"> The exception to log. </param>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Error(Exception ex, string msg, params object?[]? props);

    /// <summary>
    ///     Writes a fatal log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Fatal(string msg, params object?[]? props);

    /// <summary>
    ///     Writes a fatal log entry with an exception.
    /// </summary>
    /// <param name="ex"> The exception to log. </param>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Fatal(Exception ex, string msg, params object?[]? props);

    /// <summary>
    ///     Writes a debug log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Debug(string msg, params object?[]? props);

    /// <summary>
    ///     Writes a trace log entry.
    /// </summary>
    /// <param name="msg"> The message to log. </param>
    /// <param name="props"> Optional properties to attach to the log entry. </param>
    void Trace(string msg, params object?[]? props);
}