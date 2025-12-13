using DPorch.Logging;
using DPorch.Runtime.Python;
using DPorch.Runtime.Python.ManagedVariables;
using DPorch.Steps;
using Python.Runtime;

namespace DPorch.Runtime.Steps;

/// <summary>
///     Executes Python scripts as pipeline steps using Python.NET. Requires a 'step' function with 0 or 1 parameters. Optional 'end' function for cleanup.
/// </summary>
class PythonScriptStep(string scrName, string code, List<IManagedPythonVariable> mngedVars, ILogger log) : IScriptStep
{
    bool _isEndFunc;
    List<IManagedPythonVariable> _mngedVars = [..mngedVars];
    int _paramCount = ParameterCountNotSet;

    /// <inheritdoc />
    public CancellationToken? StepCancellationToken { get; set; }

    /// <inheritdoc />
    public void Awake()
    {
        log.Debug("Initializing Python module {Script}", scrName);

        // AddModule creates the module and executes the code immediately
        PythonGil.AddModule(scrName, code, null);
        _paramCount = GetParameterCount(scrName);

        if (_paramCount == ParameterCountNotSet)
            throw new InvalidOperationException($"No valid {StepFunctionName} function definition in {scrName}.py");

        // The end() is optional, so just keep a flag whether to call a function during end()
        _isEndFunc = PythonGil.IsFunction(scrName, EndFunctionName, EndFunctionParameterCount);

        // Ensure that only the managed variables that are defined in the script's module are getting initialized
        _mngedVars = mngedVars
            .Where(v =>
                PythonGil.IsGlobalVariable(scrName, v.Name)).ToList();
        _mngedVars
            .ForEach(v =>
                PythonGil.SetGlobalVariableValue(scrName, v.Name, v.GetInitialValue()));
    }

    /// <inheritdoc />
    public object InvokeStepFunction(object? arg)
    {
        PyObject? retVal = null;

        if (_paramCount == NoParameter)
            // Even if data is sent to this module, if a step function accepts 0 parameters, the data is ignored
            retVal = PythonGil.CallFunction(scrName, StepFunctionName);
        else if (arg is PyObject pyObj)
            // If there is data and the step function accepts 1 parameter, pass the data to the function
            retVal = PythonGil.CallFunction(scrName, StepFunctionName, pyObj);
        else if (arg == null)
            // If there is no data, but the step function accepts 1 parameter, pass None to the function
            retVal = PythonGil.CallFunction(scrName, StepFunctionName, PythonGil.None);
        else
            // If the data passed is of an unexpected type, log a fatal error and throw an exception
            throw new InvalidOperationException($"Argument is not a PyObject: {arg}");

        // Update managed variables' states to be accessed in the next iteration
        foreach (var mngedVar in _mngedVars)
        {
            var stepVal = mngedVar.GetStepValue();
            PythonGil.SetGlobalVariableValue(scrName, mngedVar.Name, stepVal);
        }

        return retVal;
    }

    /// <inheritdoc />
    public void End()
    {
        if (!_isEndFunc)
            return;

        log.Debug("Calling {Func}() in {Script}", EndFunctionName, scrName);
        PythonGil.CallFunction(scrName, EndFunctionName);
    }

    static int GetParameterCount(string srcName)
    {
        // Ensure the step function exists and allows for 0 or 1 parameters
        if (PythonGil.IsFunction(srcName, StepFunctionName, NoParameter))
            return NoParameter;
        if (PythonGil.IsFunction(srcName, StepFunctionName, 1))
            return SingleParameter;

        return ParameterCountNotSet;
    }

    #region Constants

    const int NoParameter = 0;
    const int SingleParameter = 1;
    const int ParameterCountNotSet = -1;
    const int EndFunctionParameterCount = 0;
    const string StepFunctionName = "step";
    const string EndFunctionName = "end";

    #endregion
}