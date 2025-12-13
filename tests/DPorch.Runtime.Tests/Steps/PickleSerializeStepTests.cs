using DPorch.Runtime.Python;
using DPorch.Runtime.Steps;
using DPorch.Runtime.Tests.Python;
using Python.Runtime;

namespace DPorch.Runtime.Tests.Steps;

/// <summary>
///     Tests for PickleSerializeStep serialization functionality.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PickleSerializeStepTests : IDisposable
{
    private const string ModuleName = "PickleSerializeStep";
    private readonly PickleSerializeStep _step;

    public PickleSerializeStepTests(PythonGILFixture _)
    {
        // Remove module if it exists from previous test run
        try
        {
            PythonGil.RemoveModule(ModuleName);
        }
        catch (InvalidOperationException)
        {
            // Module doesn't exist, that's fine
        }

        _step = new PickleSerializeStep();
        _step.Awake();
    }

    public void Dispose()
    {
        try
        {
            PythonGil.RemoveModule(ModuleName);
        }
        catch (InvalidOperationException)
        {
            // Module already removed or never added
        }
    }

    #region Awake() Tests

    [Fact]
    public void Awake_AddsModuleSuccessfully()
    {
        // Assert - module was added during constructor, verify serialize function exists
        Assert.True(PythonGil.IsFunction(ModuleName, "serialize", 1));
    }

    #endregion

    #region Serialize() Tests

    [Fact]
    public void Serialize_WithPyObject_ReturnsBytes()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Serialize_WithPyObject_ReturnsBytes));
        var pyObj = "test string".ToPython();

        // Act
        var result = _step.Serialize(pyObj);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Serialize_WithNull_SerializesNone()
    {
        // Act
        var result = _step.Serialize(null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Serialize_WithNonPyObject_ThrowsInvalidOperationException()
    {
        // Act & Assert - passing a plain C# string (not PyObject) should throw
        var exception = Assert.Throws<InvalidOperationException>(() => _step.Serialize("plain C# string"));

        Assert.Contains("Cannot serialize", exception.Message);
    }

    [Fact]
    public void Serialize_WithPyString_ReturnsBytes()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Serialize_WithPyString_ReturnsBytes));
        var pyString = "hello world".ToPython();

        // Act
        var result = _step.Serialize(pyString);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Serialize_WithPyDict_ReturnsBytes()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Serialize_WithPyDict_ReturnsBytes));
        using var pyDict = new PyDict();
        pyDict["key1"] = "value1".ToPython();
        pyDict["key2"] = 42.ToPython();

        // Act
        var result = _step.Serialize(pyDict);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Serialize_WithPyList_ReturnsBytes()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Serialize_WithPyList_ReturnsBytes));
        using var pyList = new PyList();
        pyList.Append("item1".ToPython());
        pyList.Append(123.ToPython());

        // Act
        var result = _step.Serialize(pyList);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Serialize_WithPyInt_ReturnsBytes()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Serialize_WithPyInt_ReturnsBytes));
        var pyInt = 12345.ToPython();

        // Act
        var result = _step.Serialize(pyInt);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    #endregion

    #region Property/Lifecycle Tests

    [Fact]
    public void End_DoesNotThrow()
    {
        // Act & Assert - should complete without throwing
        var exception = Record.Exception(() => _step.End());
        Assert.Null(exception);
    }

    [Fact]
    public void StepCancellationToken_CanSetAndGet()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        _step.StepCancellationToken = cts.Token;

        // Assert
        Assert.NotNull(_step.StepCancellationToken);
        Assert.Equal(cts.Token, _step.StepCancellationToken.Value);
    }

    #endregion
}