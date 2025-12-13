namespace DPorch.Steps;

/// <summary>
/// Defines a step that converts processed objects into serialized bytes for network transmission.
/// </summary>
public interface ISerializeStep : IStep
{
    /// <summary>
    /// Converts a processed object into serialized bytes.
    /// </summary>
    /// <param name="obj">Object to serialize, typically from the last script step.</param>
    /// <returns>Byte array containing the serialized representation, or null if obj is null.</returns>
    byte[]? Serialize(object? obj);
}