using System.Reflection;
using DPorch.CLI.Commands;
using DPorch.CLI.Preferences;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DPorch.CLI;

static class Program
{
    static int Main(string[] args)
    {
        var app = new CommandApp();
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

        if (assemblyName == null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Unable to determine application name.");
            return 1;
        }

        if (PreferencesManager.IsNewUser())
        {
            PreferencesCommand.CreatePreferencesFile();
            return 0;
        }
        
        app.Configure(config =>
        {
            config.SetApplicationName(assemblyName);

            config.AddCommand<InitCommand>(CommandsInfo.InitCommandName)
                .WithDescription(CommandsInfo.InitCommandDescription).WithExample(CommandsInfo.InitCommandExample1)
                .WithExample(CommandsInfo.InitCommandExample2).WithExample(CommandsInfo.InitCommandExample3);

            config.AddCommand<RunCommand>(CommandsInfo.RunCommandName)
                .WithDescription(CommandsInfo.RunCommandDescription).WithExample(CommandsInfo.RunCommandExample1)
                .WithExample(CommandsInfo.RunCommandExample2);

            config.AddCommand<PreferencesCommand>(CommandsInfo.PrefsCommandName)
                .WithDescription(CommandsInfo.PrefsCommandDescription).WithExample(CommandsInfo.PrefsCommandExample1)
                .WithExample(CommandsInfo.PrefsCommandExample2).WithExample(CommandsInfo.PrefsCommandExample3)
                .WithExample(CommandsInfo.PrefsCommandExample4).WithExample(CommandsInfo.PrefsCommandExample5)
                .WithExample(CommandsInfo.PrefsCommandExample6);
        });

        return app.Run(args);
    }
}