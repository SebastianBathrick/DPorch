using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DPorch.CLI.Preferences;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial record UserPreferences
{
    /// <summary>
    ///     Path to the Python DLL that enables the execution engine to run Python scripts.
    /// </summary>
    [Required(ErrorMessage = PythonDllPathRequiredError)]
    [PythonDllExists]
    [Category(PythonSettingsCategory)]
    [Display(Name = PythonDllPathName, Description = PythonDllPathDescription)]
    public string PythonDllPath { get; set; } = string.Empty;

    /// <summary>
    ///     Name of the network interface pipelines will receive input from on this machine.
    /// </summary>
    [Required(ErrorMessage = InputNetInterfaceRequiredError)]
    [Category(NetworkSettingsCategory)]
    [Display(Name = InputNetInterfaceName, Description = InputNetInterfaceDescription)]
    public string InputNetInterface { get; set; } = string.Empty;

    /// <summary>
    ///     Names of the network interfaces pipelines can send output to on this machine.
    /// </summary>
    [Required(ErrorMessage = OutputNetInterfacesRequiredError)]
    [MinLength(MinOutputNetInterfaces)]
    [Category(NetworkSettingsCategory)]
    [Display(Name = OutputNetInterfacesName, Description = OutputNetInterfacesDescription)]
    public string[] OutputNetInterfaces { get; set; } = [];

    /// <summary>
    ///     Port number used for service discovery for pipelines.
    /// </summary>
    [Range(MinDiscoveryPort, MaxDiscoveryPort, ErrorMessage = DiscoveryPortRangeError)]
    [Category(NetworkSettingsCategory)]
    [Display(Name = DiscoveryPortName, Description = DiscoveryPortDescription)]
    public int DiscoveryPort { get; set; } = DefaultDiscoveryPort;
}