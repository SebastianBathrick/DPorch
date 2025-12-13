using System.Diagnostics;
using Python.Runtime;

namespace DPorch.Runtime.Python.ManagedVariables;

/// <summary>
/// Represents a managed Python variable that provides the elapsed time (in seconds) since a script module's last
/// invocation.
/// </summary>
public class DeltaTimePythonVariable : IManagedPythonVariable
{
    const string VariableName = "delta_time";
    const double InitialValue = 0.0;

    readonly Stopwatch _stopwatch = new();

    public string Name { get; init; } = VariableName;

    /// <inheritdoc />
    /// <remarks>
    /// The initial value is always <c>0.0</c> because there was no previous invocation.
    /// </remarks>
    public PyObject GetInitialValue()
    {
        _stopwatch.Start();
        using var _ = PythonGil.Get(nameof(GetInitialValue));

        return new PyFloat(InitialValue);
    }
    
    /// <inheritdoc />
    public PyObject GetStepValue()
    {
        var lastStepTime = _stopwatch.Elapsed.TotalSeconds;
        _stopwatch.Restart();
        using var _ = PythonGil.Get(nameof(GetStepValue));

        return new PyFloat(lastStepTime);
    }
}