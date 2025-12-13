namespace DPorch.Runtime.Networking;

/// <summary>
///     Contains an input source pipeline's name and unique connection identifier.
/// </summary>
/// <param name="Name"> The display name for the input source pipeline. </param>
/// <param name="Guid"> Unique identifier for the pipeline connection. </param>
public readonly record struct InputSourcePipelineInfo(string Name, Guid Guid);