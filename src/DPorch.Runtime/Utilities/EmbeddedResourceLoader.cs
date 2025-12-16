using System.Reflection;

namespace DPorch.Runtime.Utilities;

/// <summary> 
/// Meant to load embedded resources from assembly. 
/// </summary>
public static class EmbeddedResourceLoader
{
    /// <summary>
    /// Loads an embedded resource from the assembly. 
    /// </summary>
    /// <param name="resourcePath">The path to the resource to load.</param>
    /// <returns>The content of the resource.</returns>
    public static string Load(string resourcePath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Try the exact path first
        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var availableResources = assembly.GetManifestResourceNames();

        // More helpful exception with available resources
        var resourceList = string.Join("\n  - ", availableResources);
        throw new FileNotFoundException(
            $"Embedded resource '{resourcePath}' not found.\n" +
            $"Available resources:\n  - {resourceList}\n" +
            $"Make sure the file included as 'EmbeddedResource' in the .csproj file.");
    }
}
