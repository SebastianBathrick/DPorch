using System.Text.Json;

namespace DPorch.CLI.Preferences;

partial record UserPreferences
{
    public const int DefaultDiscoveryPort = 5557;

    const int MinDiscoveryPort = 1;
    const int MaxDiscoveryPort = 65535;
    const int MinOutputNetInterfaces = 1;

    public const string PythonSettingsCategory = "Python Settings";
    public const string NetworkSettingsCategory = "Network Settings";

    public const string PythonDllPathName = "Python DLL Path";
    public const string DiscoveryPortName = "Discovery Port";
    public const string InputNetInterfaceName = "Input Network Interface";
    public const string OutputNetInterfacesName = "Output Network Interfaces";

    const string PythonDllPathDescription =
        "Path to the Python DLL that enables the execution engine to run Python scripts.";

    const string DiscoveryPortDescription = "Port number used for network discovery of pipelines.";

    const string InputNetInterfaceDescription =
        "Name of the network interface pipelines will receive input from on this machine.";

    const string OutputNetInterfacesDescription =
        "Names of the network interfaces pipelines can send output to on this machine.";

    const string PythonDllPathRequiredError = "A Python DLL path is required";
    const string InputNetInterfaceRequiredError = "A network interface is required";
    const string OutputNetInterfacesRequiredError = "At least one output network interface is required";
    const string DiscoveryPortRangeError = "The discovery port must be between 1 and 65535";
}