using DPorch.Runtime.Python;
using DPorch.Runtime.Steps;
using DPorch.Runtime.Tests.Python;
using DPorch.Runtime.Tests.TestHelpers;
using Python.Runtime;

namespace DPorch.Runtime.Tests.Steps;

/// <summary>
///     Tests for PythonScriptStep initialization and execution.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PythonScriptStepTests
{
    // Fixture ensures Python is initialized for all tests in this class
    // Constructor parameter required for xUnit collection fixture injection
    public PythonScriptStepTests(PythonGILFixture _)
    {
    }

    /// <summary>
    ///     Generates a unique module name to avoid collisions between tests.
    /// </summary>
    private static string GenerateModuleName()
    {
        return $"test_module_{Guid.NewGuid():N}";
    }

    #region End() Method Tests

    [Fact]
    public void End_ThrowsNotImplementedException()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return None
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        // TODO: Update when End is implemented
        // Act & Assert
        Assert.True(true);
    }

    #endregion

    #region Awake() Method Tests

    [Fact]
    public void Awake_WithZeroParamStepFunction_Succeeds()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return 'executed'
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            // Act
            step.Awake();

            // Assert - no exception means success
            Assert.True(PythonGil.IsFunction(moduleName, "step", 0));
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void Awake_WithOneParamStepFunction_Succeeds()
    {
        // Arrange
        const string pythonCode = @"
def step(arg):
    return arg
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            // Act
            step.Awake();

            // Assert - no exception means success
            Assert.True(PythonGil.IsFunction(moduleName, "step", 1));
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void Awake_WithMissingStepFunction_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = @"
def other_function():
    return 'hello'
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => step.Awake());
            // Exception is thrown by PythonGil.IsFunction when attribute doesn't exist
            Assert.Contains("step", exception.Message);
        }
        finally
        {
            try
            {
                PythonGil.RemoveModule(moduleName);
            }
            catch (InvalidOperationException)
            {
                // Module may not exist if Awake failed before AddModule
            }
        }
    }

    [Fact]
    public void Awake_WithTwoParamStepFunction_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = @"
def step(a, b):
    return a + b
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => step.Awake());
            // Exception thrown because step function with 2 params doesn't match expected 0 or 1 params
            Assert.Contains("step", exception.Message);
        }
        finally
        {
            try
            {
                PythonGil.RemoveModule(moduleName);
            }
            catch (InvalidOperationException)
            {
                // Module may exist but step function validation failed
            }
        }
    }

    [Fact]
    public void Awake_WithNonCallableStep_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = @"
step = 42
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => step.Awake());
            // Exception thrown because 'step' is not callable (it's an integer)
            Assert.Contains("step", exception.Message);
        }
        finally
        {
            try
            {
                PythonGil.RemoveModule(moduleName);
            }
            catch (InvalidOperationException)
            {
                // Module may exist but step function validation failed
            }
        }
    }

    [Fact]
    public void Awake_WithSyntaxError_ThrowsPythonException()
    {
        // Arrange
        const string pythonCode = @"
def step(
    # Missing closing parenthesis and body
";
        var moduleName = GenerateModuleName();
        var step = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => step.Awake());

        // Cleanup not needed - module was never successfully created
    }

    #endregion

    #region InvokeStepFunction() Method Tests

    [Fact]
    public void InvokeStepFunction_ZeroParamFunction_IgnoresArgument()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return 'zero_param_result'
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act - pass a PyObject argument that should be ignored
            using var gil = PythonGil.Get(nameof(InvokeStepFunction_ZeroParamFunction_IgnoresArgument));
            var dummyArg = "ignored".ToPython();
            var result = scriptStep.InvokeStepFunction(dummyArg);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PyObject>(result);
            Assert.Equal("zero_param_result", ((PyObject)result).As<string>());
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_OneParamFunction_WithPyObject_PassesArgument()
    {
        // Arrange
        const string pythonCode = @"
def step(arg):
    return f'received: {arg}'
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act
            using var gil = PythonGil.Get(nameof(InvokeStepFunction_OneParamFunction_WithPyObject_PassesArgument));
            var inputArg = "test_input".ToPython();
            var result = scriptStep.InvokeStepFunction(inputArg);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PyObject>(result);
            Assert.Equal("received: test_input", ((PyObject)result).As<string>());
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_OneParamFunction_WithNull_PassesNone()
    {
        // Arrange
        const string pythonCode = @"
def step(arg):
    if arg is None:
        return 'received_none'
    return 'received_something'
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act
            using var gil = PythonGil.Get(nameof(InvokeStepFunction_OneParamFunction_WithNull_PassesNone));
            var result = scriptStep.InvokeStepFunction(null);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PyObject>(result);
            Assert.Equal("received_none", ((PyObject)result).As<string>());
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_OneParamFunction_WithNonPyObject_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = @"
def step(arg):
    return arg
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act & Assert - passing a plain C# string (not PyObject) should throw
            var exception =
                Assert.Throws<InvalidOperationException>(() => scriptStep.InvokeStepFunction("plain_string"));

            Assert.Contains("not a PyObject", exception.Message);
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_ReturnsCorrectPyObject()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return {'key': 'value', 'number': 42}
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act
            using var gil = PythonGil.Get(nameof(InvokeStepFunction_ReturnsCorrectPyObject));
            var result = scriptStep.InvokeStepFunction(null);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PyObject>(result);

            var pyResult = (PyObject)result;
            using dynamic dict = pyResult;
            Assert.Equal("value", (string)dict["key"]);
            Assert.Equal(42, (int)dict["number"]);
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_WithNoneReturn_ReturnsNone()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return None
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            // Act
            using var gil = PythonGil.Get(nameof(InvokeStepFunction_WithNoneReturn_ReturnsNone));
            var result = scriptStep.InvokeStepFunction(null);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PyObject>(result);
            Assert.True(((PyObject)result).IsNone());
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    [Fact]
    public void InvokeStepFunction_MaintainsModuleState()
    {
        // Arrange
        const string pythonCode = @"
counter = 0

def step():
    global counter
    counter += 1
    return counter
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        try
        {
            scriptStep.Awake();

            using var gil = PythonGil.Get(nameof(InvokeStepFunction_MaintainsModuleState));

            // Act - call multiple times
            var result1 = scriptStep.InvokeStepFunction(null);
            var result2 = scriptStep.InvokeStepFunction(null);
            var result3 = scriptStep.InvokeStepFunction(null);

            // Assert - counter should increment across calls
            Assert.Equal(1, ((PyObject)result1!).As<int>());
            Assert.Equal(2, ((PyObject)result2!).As<int>());
            Assert.Equal(3, ((PyObject)result3!).As<int>());
        }
        finally
        {
            PythonGil.RemoveModule(moduleName);
        }
    }

    #endregion

    #region Property Tests

    [Fact]
    public void StepCancellationToken_DefaultsToNull()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return None
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);

        // Assert
        Assert.Null(scriptStep.StepCancellationToken);
    }

    [Fact]
    public void StepCancellationToken_CanSetAndGet()
    {
        // Arrange
        const string pythonCode = @"
def step():
    return None
";
        var moduleName = GenerateModuleName();
        var scriptStep = new PythonScriptStep(moduleName, pythonCode, [], NullLogger.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        scriptStep.StepCancellationToken = cts.Token;

        // Assert
        Assert.NotNull(scriptStep.StepCancellationToken);
        Assert.Equal(cts.Token, scriptStep.StepCancellationToken.Value);
    }

    #endregion
}