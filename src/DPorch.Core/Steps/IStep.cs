namespace DPorch.Steps;

/// <summary>
///     Defines the lifecycle contract for pipeline step implementations.
///     See <see href="../../../../docs/ARCHITECTURE.md#pipeline-lifecycle">ARCHITECTURE.md</see> for lifecycle details.
/// </summary>
public interface IStep
{
    /// <summary>
    ///     Gets or sets the cancellation token that signals the step to terminate long-running operations.
    /// </summary>
    CancellationToken? StepCancellationToken { get; set; }

    /// <summary>
    ///     Initializes resources required for the step to process data. Called once before iteration begins on the
    ///     pipeline thread.
    /// </summary>
    void Awake();

    /// <summary>
    ///     Performs cleanup operations and releases resources acquired during <see cref="Awake" />. Called once after
    ///     the final iteration completes.
    /// </summary>
    void End();
}