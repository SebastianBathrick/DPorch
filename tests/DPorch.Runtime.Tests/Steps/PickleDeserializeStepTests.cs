using DPorch.Runtime.Python;
using DPorch.Runtime.Steps;
using DPorch.Runtime.Tests.Python;
using Python.Runtime;

namespace DPorch.Runtime.Tests.Steps;

/// <summary>
///     Tests for PickleDeserializeStep deserialization functionality.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PickleDeserializeStepTests : IDisposable
{
    private const string DeserializeModuleName = "PickleDeserializeStep";
    private const string SerializeModuleName = "PickleSerializeStep";
    private readonly PickleDeserializeStep _deserializeStep;
    private readonly PickleSerializeStep _serializeStep;

    public PickleDeserializeStepTests(PythonGILFixture _)
    {
        // Remove modules if they exist from previous test run
        TryRemoveModule(DeserializeModuleName);
        TryRemoveModule(SerializeModuleName);

        // Initialize both steps - we need serialize to create test data
        _serializeStep = new PickleSerializeStep();
        _serializeStep.Awake();

        _deserializeStep = new PickleDeserializeStep();
        _deserializeStep.Awake();
    }

    public void Dispose()
    {
        TryRemoveModule(DeserializeModuleName);
        TryRemoveModule(SerializeModuleName);
    }

    private static void TryRemoveModule(string moduleName)
    {
        try
        {
            PythonGil.RemoveModule(moduleName);
        }
        catch (InvalidOperationException)
        {
            // Module doesn't exist or already removed
        }
    }

    /// <summary>
    ///     Helper to serialize a PyObject for use in deserialization tests.
    /// </summary>
    private byte[] SerializeObject(PyObject obj)
    {
        return _serializeStep.Serialize(obj)!;
    }

    #region Awake() Tests

    [Fact]
    public void Awake_AddsModuleSuccessfully()
    {
        // Assert - module was added during constructor, verify deserialize function exists
        Assert.True(PythonGil.IsFunction(DeserializeModuleName, "deserialize", 1));
    }

    #endregion

    #region Deserialize() Tests

    [Fact]
    public void Deserialize_WithNullMap_ReturnsNull()
    {
        // Act
        var result = _deserializeStep.Deserialize(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_WithEmptyMap_ReturnsNull()
    {
        // Arrange
        var emptyMap = new Dictionary<string, byte[]>();

        // Act
        var result = _deserializeStep.Deserialize(emptyMap);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_WithSingleSource_ReturnsPyDict()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Deserialize_WithSingleSource_ReturnsPyDict));
        var originalData = "test data".ToPython();
        var serializedData = SerializeObject(originalData);

        var sourceMap = new Dictionary<string, byte[]>
        {
            ["source1"] = serializedData
        };

        // Act
        var result = _deserializeStep.Deserialize(sourceMap);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<PyObject>(result);

        using dynamic pyResult = (PyObject)result;
        Assert.Equal("test data", (string)pyResult["source1"]);
    }

    [Fact]
    public void Deserialize_WithMultipleSources_ReturnsPyDictWithAllSources()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(Deserialize_WithMultipleSources_ReturnsPyDictWithAllSources));

        var data1 = "data from source1".ToPython();
        var data2 = "data from source2".ToPython();
        var data3 = "data from source3".ToPython();

        var sourceMap = new Dictionary<string, byte[]>
        {
            ["source1"] = SerializeObject(data1),
            ["source2"] = SerializeObject(data2),
            ["source3"] = SerializeObject(data3)
        };

        // Act
        var result = _deserializeStep.Deserialize(sourceMap);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<PyObject>(result);

        using dynamic pyResult = (PyObject)result;
        Assert.Equal("data from source1", (string)pyResult["source1"]);
        Assert.Equal("data from source2", (string)pyResult["source2"]);
        Assert.Equal("data from source3", (string)pyResult["source3"]);
    }

    [Fact]
    public void Deserialize_PreservesDataTypes()
    {
        // Arrange - serialize different Python types
        using var gil = PythonGil.Get(nameof(Deserialize_PreservesDataTypes));

        var pyString = "hello".ToPython();
        var pyInt = 42.ToPython();
        using var pyList = new PyList();
        pyList.Append(1.ToPython());
        pyList.Append(2.ToPython());
        pyList.Append(3.ToPython());

        var sourceMap = new Dictionary<string, byte[]>
        {
            ["string_source"] = SerializeObject(pyString),
            ["int_source"] = SerializeObject(pyInt),
            ["list_source"] = SerializeObject(pyList)
        };

        // Act
        var result = _deserializeStep.Deserialize(sourceMap);

        // Assert
        Assert.NotNull(result);
        using dynamic pyResult = (PyObject)result;

        // Verify types are preserved
        Assert.Equal("hello", (string)pyResult["string_source"]);
        Assert.Equal(42, (int)pyResult["int_source"]);

        // Verify list contents
        var listResult = pyResult["list_source"];
        Assert.Equal(3, (int)listResult.__len__());
        Assert.Equal(1, (int)listResult[0]);
        Assert.Equal(2, (int)listResult[1]);
        Assert.Equal(3, (int)listResult[2]);
    }

    #endregion

    #region Property/Lifecycle Tests

    [Fact]
    public void End_DoesNotThrow()
    {
        // Act & Assert - should complete without throwing
        var exception = Record.Exception(() => _deserializeStep.End());
        Assert.Null(exception);
    }

    [Fact]
    public void StepCancellationToken_CanSetAndGet()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        _deserializeStep.StepCancellationToken = cts.Token;

        // Assert
        Assert.NotNull(_deserializeStep.StepCancellationToken);
        Assert.Equal(cts.Token, _deserializeStep.StepCancellationToken.Value);
    }

    #endregion

    #region Integration Tests (Round-trip)

    [Fact]
    public void SerializeThenDeserialize_PreservesData()
    {
        // Arrange
        using var gil = PythonGil.Get(nameof(SerializeThenDeserialize_PreservesData));
        var originalValue = "round-trip test data".ToPython();

        // Act - serialize then deserialize
        var serialized = _serializeStep.Serialize(originalValue);
        Assert.NotNull(serialized);

        var sourceMap = new Dictionary<string, byte[]>
        {
            ["test_source"] = serialized
        };
        var result = _deserializeStep.Deserialize(sourceMap);

        // Assert
        Assert.NotNull(result);
        using dynamic pyResult = (PyObject)result;
        Assert.Equal("round-trip test data", (string)pyResult["test_source"]);
    }

    [Fact]
    public void SerializeThenDeserialize_WithComplexObject_PreservesStructure()
    {
        // Arrange - create a complex nested structure
        using var gil = PythonGil.Get(nameof(SerializeThenDeserialize_WithComplexObject_PreservesStructure));

        using var innerDict = new PyDict();
        innerDict["nested_key"] = "nested_value".ToPython();
        innerDict["nested_number"] = 999.ToPython();

        using var innerList = new PyList();
        innerList.Append("item1".ToPython());
        innerList.Append("item2".ToPython());

        using var complexDict = new PyDict();
        complexDict["name"] = "test object".ToPython();
        complexDict["count"] = 42.ToPython();
        complexDict["nested"] = innerDict;
        complexDict["items"] = innerList;

        // Act - serialize then deserialize
        var serialized = _serializeStep.Serialize(complexDict);
        Assert.NotNull(serialized);

        var sourceMap = new Dictionary<string, byte[]>
        {
            ["complex_source"] = serialized
        };
        var result = _deserializeStep.Deserialize(sourceMap);

        // Assert - verify entire structure is preserved
        Assert.NotNull(result);
        using dynamic pyResult = (PyObject)result;
        var restored = pyResult["complex_source"];

        // Verify top-level values
        Assert.Equal("test object", (string)restored["name"]);
        Assert.Equal(42, (int)restored["count"]);

        // Verify nested dict
        var nestedDict = restored["nested"];
        Assert.Equal("nested_value", (string)nestedDict["nested_key"]);
        Assert.Equal(999, (int)nestedDict["nested_number"]);

        // Verify nested list
        var itemsList = restored["items"];
        Assert.Equal(2, (int)itemsList.__len__());
        Assert.Equal("item1", (string)itemsList[0]);
        Assert.Equal("item2", (string)itemsList[1]);
    }

    #endregion
}