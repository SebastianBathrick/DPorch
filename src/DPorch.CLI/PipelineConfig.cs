using System.ComponentModel.DataAnnotations;

namespace DPorch.CLI;

/// <summary>
///     Configuration for a DPorch pipeline/
/// </summary>
public record PipelineConfig
{
    /// <summary>
    ///     The name of the pipeline. Must be at least 3 characters, only contain alphanumerics, hyphens, and
    ///     underscores, and must start with an alphabetic character.
    /// </summary>
    [Required(ErrorMessage = "Pipeline name is required")]
    [MinLength(3, ErrorMessage = "Pipeline name must be at least 3 characters long")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_-]*$",
        ErrorMessage =
            "Pipeline name must start with a letter and only contain letters, digits, hyphens, and underscores")]
    public required string Name { get; init; }

    /// <summary>
    ///     Paths to user scripts that process data in the pipeline. Must contain at least one .py file path
    ///     relative to the config file. Each file must be a Python (.py) file, not be empty, and exist.
    /// </summary>
    [Required(ErrorMessage = "At least one script is required")]
    [MinLength(1, ErrorMessage = "At least one script must be specified")]
    public required string[] Scripts { get; init; }

    /// <summary>
    ///     The number of input sources the pipeline expects. This property is optional.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Input source count must be at least zero")]
    public int SourcePipelineCount { get; init; }

    /// <summary>
    ///     Gets or initializes the output target pipeline(s). This property is optional.
    /// </summary>
    public string[] TargetPipelineNames { get; init; } = [];
}