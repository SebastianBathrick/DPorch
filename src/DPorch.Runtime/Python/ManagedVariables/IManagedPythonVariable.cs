using Python.Runtime;
using DPorch.Runtime.Steps;

namespace DPorch.Runtime.Python.ManagedVariables;

/// <summary>
/// 
/// </summary>
public interface IManagedPythonVariable
{
    /// <summary>
    ///     The name of the managed Python variable.
    /// </summary>
    /// <remarks>This is the same name the user will use in their code.</remarks>
    string Name { get; init; }

    /// <summary>
    ///    Gets the initial value for the managed Python variable at <see cref="PythonScriptStep.Awake()"/>
    /// </summary>
    /// <returns></returns>
    PyObject GetInitialValue();

    /// <summary>
    /// The value of the managed Python variable at step invocation in <see cref="PythonScriptStep.InvokeStepFunction(object?)"/>.
    /// </summary>
    /// <returns></returns>
    PyObject GetStepValue();
}