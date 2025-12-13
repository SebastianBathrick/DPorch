using System.Net.NetworkInformation;
using DPorch.CLI.Commands.Settings;
using DPorch.CLI.Preferences;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands;

/// <summary>
///     Command to manage user preferences.
/// </summary>
sealed class PreferencesCommand : Command<PreferencesCommandSettings>
{
    const int SuccessExitCode = 0;
    const int ErrorExitCode = 1;

    public override int Execute(CommandContext context, PreferencesCommandSettings settings)
    {
        var manager = new PreferencesManager();

        if (!manager.IsPreferencesFile() || settings.NewFile)
        {
            CreatePreferencesFile(manager);
            return SuccessExitCode;
        }

        UserPreferences prefs;

        try
        {
            prefs = manager.GetOrCreatePreferences();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to load preferences: [yellow]{0}[/]", ex.Message);
            return ErrorExitCode;
        }

        // If no flags provided, display all settings
        if (!HasAnyModificationFlag(settings))
        {
            DisplayAllPreferences(prefs);
            return SuccessExitCode;
        }

        // If the ShowAll flag is set, display all preferences
        if (settings.ShowPath)
        {
            AnsiConsole.Markup($"Preferences file path: [cyan]{manager.PreferencesFilePath}[/]\n");
            return SuccessExitCode;
        }

        // Process each flag sequentially
        if (settings.PythonDllPath != null && !TrySavePythonDll(manager, prefs, settings.PythonDllPath))
            return ErrorExitCode;

        if (settings.DiscoverPort.HasValue && !TrySaveDiscoverPort(manager, prefs, settings.DiscoverPort.Value))
            return ErrorExitCode;

        if (settings.InNetInterface && !TrySaveInputNetInterface(manager, prefs))
            return ErrorExitCode;

        if (settings.OutNetInterfaces && !TrySaveOutputNetInterfaces(manager, prefs))
            return ErrorExitCode;

        return SuccessExitCode;
    }

    public static void CreatePreferencesFile(PreferencesManager? manager = null)
    {
        manager ??= new PreferencesManager();
        var userPrefs = manager.GetOrCreatePreferences();
        manager.SavePreferences(userPrefs);
        AnsiConsole.Markup(
            "[green]Initialized:[/] Created new preferences file at [cyan]{0}[/]\n", manager.PreferencesFilePath);

        AnsiConsole.Markup("\n[yellow]Required preferences have not been assigned values[/]\n");
        AnsiConsole.Markup(
            "[dim]You can assign them now using prompts or later using the pref command with the appropriate options[/]\n\n");

        if (!AnsiConsole.Confirm("Would you like to set required preferences now?"))
            return;

        var pythonDllPath = AnsiConsole.Prompt(new TextPrompt<string>("Please enter Python v3.7+ DLL path:").Validate(s =>
        {
            if (string.IsNullOrWhiteSpace(s))
                return ValidationResult.Error("Python DLL path cannot be empty.");

            if (!s.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Error("File must be a .dll file");

            if (!File.Exists(s))
                return ValidationResult.Error("Python DLL file does not exist");

            return ValidationResult.Success();
        }));

        if (!TrySavePythonDll(manager, userPrefs, pythonDllPath))
        {
            AnsiConsole.Markup("[red]Failed to save Python DLL path. Preferences file creation incomplete.[/]\n");
            return;
        }

        if (!TrySaveInputNetInterface(manager, userPrefs))
        {
            AnsiConsole.Markup("[red]Failed to save input network interface. Preferences file creation incomplete.[/]\n");
            return;
        }

        if (!TrySaveOutputNetInterfaces(manager, userPrefs))
        {
            AnsiConsole.Markup("[red]Failed to save output network interfaces. Preferences file creation incomplete.[/]\n");
            return;
        }

        var discoveryPort = AnsiConsole.Prompt(new TextPrompt<int>("Please enter service discovery port (1-65535)")
            .DefaultValue(UserPreferences.DefaultDiscoveryPort)
            .Validate(p =>
            {
                if (p < 1 || p > 65535)
                    return ValidationResult.Error("Discovery port must be between 1 and 65535.");

                return ValidationResult.Success();
            }));

        if (!TrySaveDiscoverPort(manager, userPrefs, discoveryPort))
        {
            AnsiConsole.Markup("[red]Failed to save discovery port. Preferences file creation incomplete.[/]\n");
            return;
        }

        AnsiConsole.Markup("[green]Preferences file setup complete![/]\n");
    }

    static bool TrySavePythonDll(PreferencesManager manager, UserPreferences prefs, string path)
    {
        try
        {
            prefs.PythonDllPath = path;
            manager.SavePreferences(prefs);
            AnsiConsole.MarkupLine("[green]Saved:[/] Python DLL path: [cyan]{0}[/]", path);

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to save Python DLL path: [yellow]{0}[/]", ex.Message);

            return false;
        }
    }

    static bool TrySaveDiscoverPort(PreferencesManager manager, UserPreferences prefs, int port)
    {
        try
        {
            prefs.DiscoveryPort = port;
            manager.SavePreferences(prefs);
            AnsiConsole.MarkupLine("[green]Saved:[/] Discovery port: [cyan]{0}[/]", port);

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to save discovery port: [yellow]{0}[/]", ex.Message);

            return false;
        }
    }

    static bool TrySaveInputNetInterface(PreferencesManager manager, UserPreferences prefs)
    {
        try
        {
            var interfaces = GetNetworkInterfaceNames();

            if (interfaces.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No network interfaces found.");

                return false;
            }

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("Select input network interface:") 
                    .AddChoices(interfaces));

            prefs.InputNetInterface = selected;
            manager.SavePreferences(prefs);
            AnsiConsole.MarkupLine("[green]Saved:[/] Input network interface: [cyan]{0}[/]", selected);

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Failed to save input network interface: [yellow]{0}[/]", ex.Message);

            return false;
        }
    }

    static bool TrySaveOutputNetInterfaces(PreferencesManager manager, UserPreferences prefs)
    {
        try
        {
            var interfaces = GetNetworkInterfaceNames();

            if (interfaces.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No network interfaces found.");

                return false;
            }

            var selected = AnsiConsole.Prompt(new MultiSelectionPrompt<string>()
                .Title("Select output network interfaces:").Required().AddChoices(interfaces));

            prefs.OutputNetInterfaces = selected.ToArray();
            manager.SavePreferences(prefs);
            AnsiConsole.MarkupLine("[green]Saved:[/] Output network interfaces: [cyan]{0}[/]",
                string.Join(", ", selected));

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Failed to save output network interfaces: [yellow]{0}[/]",
                ex.Message);

            return false;
        }
    }

    static bool HasAnyModificationFlag(PreferencesCommandSettings settings)
    {
        return settings.PythonDllPath != null ||
               settings.DiscoverPort.HasValue ||
               settings.InNetInterface ||
               settings.OutNetInterfaces ||
               settings.ShowPath;
    }

    static void DisplayAllPreferences(UserPreferences prefs)
    {
        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[bold underline]Current Preferences:[/]");
        AnsiConsole.MarkupLine("");

        var pythonDllDisplay = !string.IsNullOrWhiteSpace(prefs.PythonDllPath)
            ? prefs.PythonDllPath
            : "[dim](not set)[/]";
        AnsiConsole.MarkupLine($"  [bold]Python DLL Path:[/] {pythonDllDisplay}");
        AnsiConsole.MarkupLine($"  [bold]Discovery Port:[/] {prefs.DiscoveryPort}");

        var inputInterfaceDisplay = !string.IsNullOrWhiteSpace(prefs.InputNetInterface)
            ? prefs.InputNetInterface
            : "[dim](not set)[/]";
        AnsiConsole.MarkupLine($"  [bold]Input Network Interface:[/] {inputInterfaceDisplay}");

        var outputInterfacesDisplay = prefs.OutputNetInterfaces.Length > 0
            ? string.Join(", ", prefs.OutputNetInterfaces)
            : "[dim](not set)[/]";
        AnsiConsole.MarkupLine($"  [bold]Output Network Interfaces:[/] {outputInterfacesDisplay}");
        AnsiConsole.MarkupLine("");
    }

    static string[] GetNetworkInterfaceNames()
    {
        return NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up)
            .Select(ni => ni.Name).ToArray();
    }
}