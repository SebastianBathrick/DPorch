using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands.Settings;

/// <summary>
///     Settings for the init command.
/// </summary>
public sealed class InitCommandSettings : CommandSettings
{
    const char UnderscoreChar = '_';
        
    [Description(CommandsInfo.InitNameDescription)]
    [CommandOption(CommandsInfo.InitNameOption)]
    public string? Name { get; init; }

    [Description(CommandsInfo.InitScriptsDescription)]
    [CommandOption(CommandsInfo.InitScriptsOption)]
    public string[] Scripts { get; init; } = [];

    [Description(CommandsInfo.InitInputSourceCountDescription)]
    [CommandOption(CommandsInfo.InitInputSourceCountOption)]
    public int? InputSourceCount { get; init; }

    [Description(CommandsInfo.InitOutputTargetsDescription)]
    [CommandOption(CommandsInfo.InitOutputTargetsOption)]
    public string[] OutputTargets { get; init; } = [];

    [Description(CommandsInfo.InitPathsDescription)]
    [CommandOption(CommandsInfo.InitPathOption)]
    public string[] Paths { get; init; } = [];
        
    
    public override ValidationResult Validate()
    {
        // Validate InputSourceCount is non-negative if provided
        if (InputSourceCount.HasValue && InputSourceCount.Value < 0)
            return ValidationResult.Error("Input source count cannot be negative");

        // Validate pipeline name format if provided
        if (!string.IsNullOrEmpty(Name) && !IsValidPipelineName(Name, out var errorMessage))
            return ValidationResult.Error(errorMessage);

        return ValidationResult.Success();
    }

    private static bool IsValidPipelineName(string name, out string errorMessage)
    {
        // Pipeline name cannot start with a digit
        if (char.IsDigit(name[0]))
        {
            errorMessage = "Pipeline name cannot start with a digit";

            return false;
        }

        // Pipeline name can only contain letters, digits, and underscores
        if (!name.All(c => char.IsLetterOrDigit(c) || c == UnderscoreChar))
        {
            errorMessage = "Pipeline name can only contain letters, digits, and underscores";

            return false;
        }

        errorMessage = string.Empty;

        return true;
    }
}
