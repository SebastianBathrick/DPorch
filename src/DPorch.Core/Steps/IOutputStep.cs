namespace DPorch.Steps;

/// <summary>
/// Defines a step that sends serialized output data to target pipelines.
/// </summary>
public interface IOutputStep : IStep
{
    /// <summary>
    /// Transmits serialized data to all configured target pipelines.
    /// </summary>
    /// <param name="serializedOutput">Serialized data to transmit, or null if no data is available.</param>
    void Send(byte[]? serializedOutput);
}