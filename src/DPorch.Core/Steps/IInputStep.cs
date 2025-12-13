namespace DPorch.Steps;

/// <summary>
///     Defines a step that receives serialized input data from source pipelines.
/// </summary>
public interface IInputStep : IStep
{
    /// <summary>
    ///     Receives serialized data from all expected source pipelines. Blocks until data from all sources is
    ///     available.
    /// </summary>
    /// <returns>Dictionary mapping source pipeline names to byte data, or null if unavailable.</returns>
    Dictionary<string, byte[]>? Receive();
}