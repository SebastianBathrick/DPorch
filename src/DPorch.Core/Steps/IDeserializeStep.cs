namespace DPorch.Steps;

/// <summary>
///     Defines a step that converts serialized bytes into objects for processing.
/// </summary>
public interface IDeserializeStep : IStep
{
    /// <summary>
    ///     Converts serialized bytes from source pipelines into an object.
    /// </summary>
    /// <param name="inSrcPipelineMap">Dictionary mapping source pipeline names to their byte data.</param>
    /// <returns>Deserialized object to pass to the first script step, or null.</returns>
    object? Deserialize(Dictionary<string, byte[]>? inSrcPipelineMap);
}