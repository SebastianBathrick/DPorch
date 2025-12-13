using DPorch.Runtime.Python;
using DPorch.Steps;
using Python.Runtime;

namespace DPorch.Runtime.Steps;

/// <summary>
///     Deserializes byte arrays into Python objects using Python's pickle module.
/// </summary>
public class PickleDeserializeStep : IDeserializeStep
{
    const string DeserializeFunctionName = "deserialize";

    const string PythonCode = @"
import pickle

def deserialize(source_msg_map):
    """"""Deserialize pickled byte data from each source into a dictionary.

    Args:
        source_msg_map: dict[str, bytes] - Dictionary mapping source names to pickled byte data

    Returns:
        dict[str, Any] - Dictionary mapping source names to deserialized Python objects
    """"""
    result = {}
    for source, data in source_msg_map.items():
        result[source] = pickle.loads(data)
    return result";

    readonly string _name = nameof(PickleDeserializeStep);

    /// <inheritdoc />
    public CancellationToken? StepCancellationToken { get; set; }

    /// <inheritdoc />
    public void Awake()
    {
        PythonGil.AddModule(_name, PythonCode, null);
    }

    /// <inheritdoc />
    public object? Deserialize(Dictionary<string, byte[]>? sourceMsgMap)
    {
        if (sourceMsgMap == null || sourceMsgMap.Count == 0)
            return null;

        using var gil = PythonGil.Get(nameof(Deserialize));

        // Convert C# dictionary to Python dictionary with byte arrays
        using var pyDict = new PyDict();

        foreach (var (source, data) in sourceMsgMap)
        {
            // Convert byte[] to Python bytes object
            using var pyBytes = data.ToPython();
            pyDict[source.ToPython()] = pyBytes;
        }

        // Call Python deserialize function
        var result = PythonGil.CallFunction(_name, DeserializeFunctionName, pyDict);

        return result;
    }

    /// <inheritdoc />
    public void End()
    {
    }
}