using System.Text.Json;
using DPorch.CLI.Commands.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands;

/// <summary>
///     Command to initialize one or more pipeline configuration files.
/// </summary>
sealed class InitCommand : Command<InitCommandSettings>
{
    
    const string JsonFileExtension = ".json";
    const string DefaultConfigFilename = "config" + JsonFileExtension;

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public override int Execute(CommandContext context, InitCommandSettings settings)
    {
        var outPath = DetermineOutputPath(settings);

        if (!TryCreateConfigFile(settings, outPath))
            return 1;

        return 0;
    }

    string DetermineOutputPath(InitCommandSettings settings)
    {
        if (string.IsNullOrEmpty(settings.Name))
            return DefaultConfigFilename;

        // If a pipeline name is provided - use pipeline name as filename
        var outputPath = $"{settings.Name}{JsonFileExtension}";

        // If a file exists, fall back to the default name
        if (File.Exists(outputPath))
            outputPath = DefaultConfigFilename;

        return outputPath;
    }

    bool TryCreateConfigFile(InitCommandSettings settings, string outputPath)
    {
        try
        {
            if (!File.Exists(outputPath))
            {
                // Create config with user-specified values or defaults
                var config = CreatePipelineConfig(settings);
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(outputPath, json);

                AnsiConsole.MarkupLine("[green]Created:[/] [cyan]{0}[/]", Path.GetFullPath(outputPath));

                return true;
            }

            // If file already exists and prompt for confirmation
            return IsUserOverwritingFile(outputPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to create [cyan]{0}[/]: [yellow]{1}[/]", outputPath,
                ex.Message);

            return false;
        }
    }

    static bool IsUserOverwritingFile(string outputPath)
    {
        AnsiConsole.MarkupLine("[yellow]Warning:[/] File already exists: [cyan]{0}[/]", outputPath);

        if (AnsiConsole.Confirm($"Overwrite {outputPath}?", false))
            return false;

        AnsiConsole.MarkupLine("[cyan]Skipped:[/] [dim]{0}[/]", outputPath);

        return true;
    }

    static PipelineConfig CreatePipelineConfig(InitCommandSettings settings)
    {
        return new PipelineConfig
        {
            Name = settings.Name ?? string.Empty,
            Scripts = settings.Scripts,
            SourcePipelineCount = settings.InputSourceCount ?? 0,
            TargetPipelineNames = settings.OutputTargets
        };
    }
}