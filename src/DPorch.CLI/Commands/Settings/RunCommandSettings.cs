using System.ComponentModel;
using DPorch.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands.Settings;

/// <summary>
///     Settings for the run command.
/// </summary>
public sealed class RunCommandSettings : CommandSettings
{
    const LogLevel DefaultLogLevel = LogLevel.Info;
    const string JsonFileExtension = ".json";

    static readonly IReadOnlyDictionary<string, LogLevel> LogLevelStringMap =
        new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase)
        {
            { GetLogLevelString(LogLevel.Info), LogLevel.Info },
            { GetLogLevelString(LogLevel.Debug), LogLevel.Debug },
            { GetLogLevelString(LogLevel.Trace), LogLevel.Trace },
            { GetLogLevelString(LogLevel.Warning), LogLevel.Warning },
            { GetLogLevelString(LogLevel.Error), LogLevel.Error },
            { GetLogLevelString(LogLevel.Fatal), LogLevel.Fatal },
            { GetLogLevelString(LogLevel.None), LogLevel.None }
        };

    [Description(CommandsInfo.RunLogLevelDescription)]
    [CommandOption(CommandsInfo.RunLogLevelOption)]
    public string? MinimumLogLevel { get; init; }

    [Description(CommandsInfo.RunConfigsDescription)]
    [CommandArgument(0, CommandsInfo.RunConfigsArgument)]
    public string[] ConfigFilePaths { get; init; } = Array.Empty<string>();

    public LogLevel MinimumLogLevelEnum { get; private set; } = DefaultLogLevel;

    public override ValidationResult Validate()
    {
        if (ConfigFilePaths.Length == 0)
            return ValidationResult.Error("At least one configuration file must be specified");

        foreach (var file in ConfigFilePaths)
            if (!IsValidConfigFilePath(file, out var errMsg))
                return ValidationResult.Error(errMsg);

        if (MinimumLogLevel == null)
            return ValidationResult.Success();

        if (!LogLevelStringMap.TryGetValue(MinimumLogLevel, out var lvl))
            return ValidationResult.Error(
                $"Invalid log level: {MinimumLogLevel}. Valid values are: {string.Join(", ", LogLevelStringMap.Keys)}");

        MinimumLogLevelEnum = lvl;

        return ValidationResult.Success();

    }

    static bool IsValidConfigFilePath(string path, out string errMsg)
    {
        errMsg = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                errMsg = "[red]File path cannot be empty or whitespace[/]";
                return false;
            }

            var fileInfo = new FileInfo(path);

            if (!fileInfo.Exists)
            {
                errMsg = $"File does not exist: {path}";
                return false;
            }

            if (Path.HasExtension(path) && fileInfo.Extension.Equals(JsonFileExtension, StringComparison.CurrentCultureIgnoreCase))
                return true;

            errMsg = $"File must have a {JsonFileExtension} extension: {path}";
            return false;

        }
        catch (Exception ex)
        {
            errMsg = ex.Message;
            return false;
        }
    }

    public bool IsLogLevelAssignment()
    {
        return !string.IsNullOrWhiteSpace(MinimumLogLevel);
    }

    static string GetLogLevelString(LogLevel lvl)
    {
        return lvl.ToString().ToLower();
    }
}