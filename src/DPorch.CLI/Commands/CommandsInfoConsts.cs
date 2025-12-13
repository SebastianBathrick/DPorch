namespace DPorch.CLI.Commands;

internal static class CommandsInfo
{
    #region InitCommand

    public const string InitCommandName = "init";
    public const string InitCommandDescription = "Create a new pipeline configuration file";
    public const string InitCommandExample1 = "init";
    public const string InitCommandExample2 = "init --name my_pipeline";
    public const string InitCommandExample3 = "init -n my_pipeline -s script.py -i 2 -o target1";

    // Settings Descriptions
    public const string InitNameDescription = "Name of the pipeline";
    public const string InitScriptsDescription = "Python script file(s) for the pipeline";
    public const string InitInputSourceCountDescription = "Number of input sources the pipeline expects";
    public const string InitOutputTargetsDescription = "Output target pipeline(s)";
    public const string InitPathsDescription = "Output file path(s) for the configuration";

    // Settings Options
    public const string InitNameOption = "-n|--name";
    public const string InitScriptsOption = "-s|--scripts";
    public const string InitInputSourceCountOption = "-i|--input-source-count";
    public const string InitOutputTargetsOption = "-o|--output-targets";
    public const string InitPathOption = "-p|--path";

    #endregion

    #region RunCommand

    public const string RunCommandName = "run";
    public const string RunCommandDescription = "Execute a pipeline configuration";
    public const string RunCommandExample1 = "run pipeline.json";
    public const string RunCommandExample2 = "run pipeline_1.json pipeline_2.json";

    // Settings Descriptions
    public const string RunLogLevelDescription = "Set minimum log level";
    public const string RunConfigsDescription = "Pipeline configuration file(s) to execute";

    // Settings Options
    public const string RunLogLevelOption = "-l|--log-level";
    public const string RunConfigsArgument = "[configs]";

    #endregion

    #region PrefsCommand

    public const string PrefsCommandName = "prefs";
    public const string PrefsCommandDescription = "Manage user preferences. Omit options to view all preferences.";
    public const string PrefsCommandExample1 = "prefs -p C:\\Python311\\python311.dll";
    public const string PrefsCommandExample2 = "prefs -d 5557";
    public const string PrefsCommandExample3 = "prefs -i";
    public const string PrefsCommandExample4 = "prefs -o";
    public const string PrefsCommandExample5 = "prefs -f";
    public const string PrefsCommandExample6 = "prefs";

    // Settings Descriptions
    public const string PrefsNewFileDescription = "Create a new preferences file, overwriting existing one if present";
    public const string PrefsPythonDllPathDescription = "Path to the Python DLL";
    public const string PrefsInNetInterfaceDescription = "Select input network interface";
    public const string PrefsOutNetInterfacesDescription = "Select output network interfaces";
    public const string PrefsDiscoverPortDescription = "Discovery port number (1-65535)";
    public const string PrefsShowPathDescription = "Display the preferences file path";

    // Settings Options
    public const string PrefsNewFileOption = "-n|--new-file";
    public const string PrefsPythonDllOption = "-p|--python-dll";
    public const string PrefsInNetInterfaceOption = "-i|--in-net-iface";
    public const string PrefsOutNetInterfacesOption = "-o|--out-net-ifaces";
    public const string PrefsDiscoverPortOption = "-d|--discover-port";
    public const string PrefsShowPathOption = "-f|--file";

    #endregion
}
