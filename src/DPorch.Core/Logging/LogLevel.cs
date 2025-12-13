namespace DPorch.Logging;

/// <summary>
///     Defines log event severity levels.
/// </summary>
public enum LogLevel
{
    /// <summary>
    ///     Logs that contain the most detailed messages. These messages may contain sensitive application data.
    /// </summary>
    Trace = 0,

    /// <summary>
    ///     Logs used for interactive investigation during development.
    /// </summary>
    Debug = 1,

    /// <summary>
    ///     Logs that track the general flow of the application.
    /// </summary>
    Info = 2,

    /// <summary>
    ///     Logs that highlight an abnormal or unexpected event in the application flow.
    /// </summary>
    Warning = 4,

    /// <summary>
    ///     Logs that highlight when the current flow of execution is stopped due to a failure.
    /// </summary>
    Error = 5,

    /// <summary>
    ///     Logs that describe an unrecoverable application or system crash.
    /// </summary>
    Fatal = 6,

    /// <summary>
    ///     Not used for writing log messages. Specifies that no messages should be logged.
    /// </summary>
    None = 7
}