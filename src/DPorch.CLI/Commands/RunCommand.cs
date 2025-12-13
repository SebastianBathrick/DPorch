using System.Diagnostics;
using System.Text.Json;
using DPorch.CLI.Commands.Settings;
using DPorch.CLI.Preferences;
using DPorch.Logging;
using DPorch.Runtime;
using DPorch.Runtime.Python;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI.Commands;

/// <summary>
///     Command to execute one or more pipeline configurations.
/// </summary>
public sealed class RunCommand : Command<RunCommandSettings>
{
    #region Constants & Static Readonlys

    const int ErrorExitCode = 1;
    const int SuccessExitCode = 0;
    const int ExitTimeoutSeconds = 10;
    const string RunCommandName = "run";
    const string FallbackMainModuleFileName = "dporch";

    #endregion

    #region Execution

    public override int Execute(CommandContext context, RunCommandSettings settings)
    {
        if (settings.ConfigFilePaths.Length > 1 && !StartAdditionalPipelineProcesses(settings.ConfigFilePaths))
            return ErrorExitCode;

        return ExecuteAsync(settings).GetAwaiter().GetResult();
    }

    async Task<int> ExecuteAsync(RunCommandSettings settings)
    {
        var pipe = await BuildPipelineFromConfig(settings.ConfigFilePaths[0], settings);

        if (pipe == null)
        {
            AnsiConsole.MarkupLine("[dim]Aborting...[/]");
            return ErrorExitCode;
        }

        using var cts = new CancellationTokenSource();
        SetupCancellationHandler(cts);

        var exitTcs = new TaskCompletionSource();

        if (!await StartPipeline(pipe, cts.Token, exitTcs))
            return ErrorExitCode;

        return await WaitForPipelineCompletion(exitTcs, cts.Token);
    }

    static async Task<bool> StartPipeline(IPipeline pipe, CancellationToken cancelTkn, TaskCompletionSource exitTcs)
    {
        if (await pipe.TryStart(exitTcs, cancelTkn))
            return true;

        AnsiConsole.MarkupLine("[red]Error:[/] Failed to start pipeline");
        return false;
    }

    static async Task<int> WaitForPipelineCompletion(TaskCompletionSource pipeExitTcs, CancellationToken cancelTkn)
    {
        try
        {
            await pipeExitTcs.Task;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Pipeline terminated due to an error: {0}", ex.Message);
            return ErrorExitCode;
        }

        if (pipeExitTcs.Task.IsFaulted)
        {
            var exception = pipeExitTcs.Task.Exception?.Flatten();
            AnsiConsole.MarkupLine("[red]Error:[/] Pipeline faulted: {0}", exception?.Message ?? "Unknown error");
            return ErrorExitCode;
        }

        if (!pipeExitTcs.Task.IsCompleted)
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ExitTimeoutSeconds), cancelTkn);
            var completedTask = await Task.WhenAny(pipeExitTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                AnsiConsole.MarkupLine(
                    "[red]Error:[/] Pipeline did not terminate within [cyan]{0}[/] seconds. Forcing exit.", ExitTimeoutSeconds);
                return ErrorExitCode;
            }
        }

        AnsiConsole.MarkupLine("[green] Pipeline completed successfully [/]");
        return SuccessExitCode;
    }

    static void SetupCancellationHandler(CancellationTokenSource? cts)
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            AnsiConsole.MarkupLine("[yellow]Cancellation requested...[/]");
            cts?.Cancel();
            eventArgs.Cancel = true;
        };
    }

    #endregion

    #region Pipeline Building

    static async Task<IPipeline?> BuildPipelineFromConfig(string configPath, RunCommandSettings settings)
    {
        var prefs = LoadUserPreferences();

        if (prefs == null)
            return null;

        var config = await LoadPipelineConfig(configPath);

        if (config == null)
            return null;

        if (settings.IsLogLevelAssignment())
            ILogger.DefaultMinimumLogLevel = settings.MinimumLogLevelEnum;

        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();

        return await BuildPipeline(config, prefs, configDir);
    }

    static UserPreferences? LoadUserPreferences()
    {
        var prefsMng = new PreferencesManager();

        if (prefsMng.IsPreferencesFile())
            return prefsMng.LoadPreferences();

        AnsiConsole.MarkupLine(
            "[red]Error:[/] Preferences file not found. Run the [cyan]prefs[/] command to create preferences.");
        return null;

    }

    static async Task<PipelineConfig?> LoadPipelineConfig(string configPath)
    {
        try
        {
            var configJson = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<PipelineConfig>(configJson, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            if (config == null)
                AnsiConsole.MarkupLine("[red]Error:[/] Pipeline configuration deserialized to null.");

            return config;
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Pipeline configuration is malformed: [yellow]{0}[/]", ex.Message);
            return null;
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Failed to read pipeline configuration file: [yellow]{0}[/]", ex.Message);
            return null;
        }
    }
    
    static async Task<IPipeline> BuildPipeline(PipelineConfig config, UserPreferences prefs, string configDir)
    {
        var sp = RuntimeServices.GetServiceProvider();
        
        // TODO: Handle this dependency better - consider dependency injection
        PythonGil.Initialize(prefs.PythonDllPath, configDir, sp.GetRequiredService<ILogger>());
        
        var builder = sp.GetRequiredService<IPipelineBuilder>();

        ConfigureInputStep(builder, config, prefs);
        await ConfigureScriptSteps(builder, config, configDir);
        ConfigureOutputStep(builder, config, prefs);

        return builder.SetName(config.Name).Build();
    }

    static void ConfigureInputStep(IPipelineBuilder builder, PipelineConfig config, UserPreferences prefs)
    {
        if (config.SourcePipelineCount == 0)
            return;

        builder
            .SetInputStep(
                config.Name,
                config.SourcePipelineCount,
                prefs.DiscoveryPort,
                prefs.InputNetInterface,
                prefs.OutputNetInterfaces)
            .SetDeserializeStep();
    }

    static async Task ConfigureScriptSteps(IPipelineBuilder builder, PipelineConfig config, string configDir)
    {
        foreach (var scrRelPath in config.Scripts)
        {
            var scrAbsPath = Path.Combine(configDir, scrRelPath);

            if (!File.Exists(scrAbsPath))
                throw new FileNotFoundException($"Script file not found: {scrAbsPath}");

            if (!Path.GetExtension(scrAbsPath).Equals(".py", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Script file must be a Python file: {scrAbsPath}");

            var scrName = Path.GetFileName(scrAbsPath);
            var code = await File.ReadAllTextAsync(scrAbsPath);

            builder.AddScriptStep(scrName, code);
        }
    }

    static void ConfigureOutputStep(IPipelineBuilder builder, PipelineConfig config, UserPreferences prefs)
    {
        if (config.TargetPipelineNames.Length == 0)
            return;

        builder
            .SetSerializeStep()
            .SetOutputStep(config.Name, config.TargetPipelineNames, prefs.DiscoveryPort, prefs.OutputNetInterfaces);
    }

    #endregion

    #region Multi-Process Management

    bool StartAdditionalPipelineProcesses(string[] configPaths)
    {
        AnsiConsole.MarkupLine("[blue]Info:[/] Starting additional pipeline processes...");

        var startedProcs = new List<Process>();

        try
        {
            // Skip the first config - it will be executed by the current process
            foreach (var configPath in configPaths.Skip(1))
            {
                var process = LaunchPipelineProcess(configPath);
                startedProcs.Add(process);
            }

            AnsiConsole.MarkupLine(
                "[green]Started[/] [cyan]{0}[/] additional pipeline process(es)", startedProcs.Count);

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                "[red]Error:[/] Failed to start pipeline process: [yellow]{0}[/]",
                ex.Message);

            // Clean up any processes that were successfully started
            TerminateProcesses(startedProcs);

            return false;
        }
    }

    static Process LaunchPipelineProcess(string configPath)
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? FallbackMainModuleFileName;
        var args = BuildProcessArguments(RunCommandName, configPath);

        var proc = Process.Start(exePath, args);

        if (proc == null)
            throw new InvalidOperationException($"Failed to start process for configuration: {configPath}");

        return proc;
    }

    static string BuildProcessArguments(params string[] arguments)
    {
        // Quote arguments containing whitespace
        var quotedArguments = arguments.Select(arg =>
            arg.Contains(' ') ? $"\"{arg}\"" : arg);

        return string.Join(' ', quotedArguments);
    }

    static void TerminateProcesses(IEnumerable<Process> procs)
    {
        foreach (var process in procs)
            try
            {
                if (!process.HasExited) process.Kill();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] Failed to terminate process [cyan]{0}[/]: [dim]{1}[/]",
                    process.Id, ex.Message);
            }
    }

    #endregion
}