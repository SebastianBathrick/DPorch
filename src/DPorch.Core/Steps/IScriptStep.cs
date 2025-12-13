namespace DPorch.Steps;

/// <summary>
/// Defines a step that executes user-defined processing logic on data during each iteration.
/// </summary>
public interface IScriptStep : IStep
{
    /// <summary>
    /// Executes the user-defined processing function with the provided input data.
    /// </summary>
    /// <param name="arg">Input data from the previous step, or null if no data is available.</param>
    /// <returns>Transformed data to pass to the next step, or null if the function produces no output.</returns>
    object? InvokeStepFunction(object? arg);
}