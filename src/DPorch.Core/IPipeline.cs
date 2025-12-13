using DPorch.Logging;
using DPorch.Steps;

namespace DPorch;

/// <summary>
///     Defines a data processing pipeline that executes sequential steps on an isolated thread. See
///     <see href="../../../docs/ARCHITECTURE.md#pipeline-lifecycle">ARCHITECTURE.md</see> for lifecycle details.
/// </summary>
public interface IPipeline
{
    /// <summary>
    ///     Sets the pipeline identifier used for network discovery and communication. Must be at least 3
    ///     characters, contain only alphanumerics/hyphens/underscores, and start with a letter.
    /// </summary>
    /// <param name="name">The pipeline identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline name has already been assigned.</exception>
    void SetName(string name);

    /// <summary>
    ///     Sets the step that receives serialized input from network sources. Must be paired with deserialize step.
    /// </summary>
    /// <param name="inputStep">The input step instance, or null if not used.</param>
    /// <exception cref="InvalidOperationException">Thrown when the input step has already been assigned.</exception>
    internal void SetInputStep(IInputStep? inputStep);

    /// <summary>
    ///     Sets the step that converts processed objects to bytes for network transmission. Must be paired with output step.
    /// </summary>
    /// <param name="serializeStep">The serialize step instance, or null if not used.</param>
    /// <exception cref="InvalidOperationException">Thrown when the serialize step has already been assigned.</exception>
    internal void SetSerializeStep(ISerializeStep? serializeStep);

    /// <summary>
    ///     Adds a script step that processes data sequentially. At least one script step is required.
    /// </summary>
    /// <param name="scriptStep">The script step to add to the processing chain.</param>
    internal void AddScriptStep(IScriptStep scriptStep);

    /// <summary>
    ///     Sets the step that converts received bytes to objects for processing. Must be paired with input step.
    /// </summary>
    /// <param name="deserializeStep">The deserialize step instance, or null if not used.</param>
    /// <exception cref="InvalidOperationException">Thrown when the deserialize step has already been assigned.</exception>
    internal void SetDeserializeStep(IDeserializeStep? deserializeStep);

    /// <summary>
    ///     Sets the step that sends serialized output to network targets. Must be paired with serialize step.
    /// </summary>
    /// <param name="outputStep">The output step instance, or null if not used.</param>
    /// <exception cref="InvalidOperationException">Thrown when the output step has already been assigned.</exception>
    internal void SetOutputStep(IOutputStep? outputStep);

    /// <summary>
    ///     Sets the logger for pipeline logging operations.
    /// </summary>
    /// <param name="logger">The logger instance to use for pipeline logging.</param>
    /// <exception cref="InvalidOperationException">Thrown when the logger has already been assigned.</exception>
    internal void SetLogger(ILogger logger);

    /// <summary>
    ///     Validates the pipeline configuration and starts its lifecycle on a dedicated execution thread.
    /// </summary>
    /// <param name="exitTcs">Task completion source that signals when the execution thread has completed.</param>
    /// <param name="cancelTkn">
    ///     Cancellation token that signals the pipeline to stop after completing the current
    ///     iteration.
    /// </param>
    /// <returns>True if the pipeline configuration is valid and the thread started successfully; otherwise, false.</returns>
    public async Task<bool> TryStart(TaskCompletionSource exitTcs, CancellationToken cancelTkn)
    {
        await Task.CompletedTask;

        throw new NotImplementedException($"{nameof(TryStart)} not implemented");
    }
}