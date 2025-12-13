using System.ComponentModel.DataAnnotations;

namespace DPorch.CLI.Preferences;

/// <summary>
///     Validates that a file path points to an existing Python DLL.
/// </summary>
public class PythonDllExistsAttribute()
    : ValidationAttribute("The Python DLL at '{0}' does not exist or is not a valid DLL file.")
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return new ValidationResult("Python DLL path cannot be empty.");

        var filePath = value.ToString()!;

        if (!File.Exists(filePath))
            return new ValidationResult($"The Python DLL at '{filePath}' does not exist.",
                [validationContext.MemberName ?? "PythonDllPath"]);

        if (filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Success;

        return new ValidationResult($"The file at '{filePath}' is not a DLL file.",
            [validationContext.MemberName ?? "PythonDllPath"]);
    }
}