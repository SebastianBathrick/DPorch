using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands.Settings;

/// <summary>
///     Settings for the prefs command.
/// </summary>
public sealed class PreferencesCommandSettings : CommandSettings
{
    const string DllExtension = ".dll";
    const int MinPort = 1;
    const int MaxPort = 65535;

    [Description(CommandsInfo.PrefsNewFileDescription)]
    [CommandOption(CommandsInfo.PrefsNewFileOption)]
    public bool NewFile { get; init; }
    
    [Description(CommandsInfo.PrefsPythonDllPathDescription)]
    [CommandOption(CommandsInfo.PrefsPythonDllOption)]
    public string? PythonDllPath { get; init; }

    [Description(CommandsInfo.PrefsInNetInterfaceDescription)]
    [CommandOption(CommandsInfo.PrefsInNetInterfaceOption)]
    public bool InNetInterface { get; init; }

    [Description(CommandsInfo.PrefsOutNetInterfacesDescription)]
    [CommandOption(CommandsInfo.PrefsOutNetInterfacesOption)]
    public bool OutNetInterfaces { get; init; }

    [Description(CommandsInfo.PrefsDiscoverPortDescription)]
    [CommandOption(CommandsInfo.PrefsDiscoverPortOption)]
    public int? DiscoverPort { get; init; }

    [Description(CommandsInfo.PrefsShowPathDescription)]
    [CommandOption(CommandsInfo.PrefsShowPathOption)]
    public bool ShowPath { get; init; }

    public override ValidationResult Validate()
    {
        // Validate PythonDllPath if provided
        if (PythonDllPath != null)
        {
            if (string.IsNullOrWhiteSpace(PythonDllPath))
                return ValidationResult.Error("Python DLL path cannot be empty.");

            if (!File.Exists(PythonDllPath))
                return ValidationResult.Error($"Python DLL file does not exist: {PythonDllPath}");

            if (!PythonDllPath.EndsWith(DllExtension, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Error($"File must be a .dll file: {PythonDllPath}");
        }

        // Validate DiscoverPort if provided
        if (DiscoverPort.HasValue)
            if (DiscoverPort.Value < MinPort || DiscoverPort.Value > MaxPort)
                return ValidationResult.Error($"Discovery port must be between {MinPort} and {MaxPort}.");

        return ValidationResult.Success();
    }
}
