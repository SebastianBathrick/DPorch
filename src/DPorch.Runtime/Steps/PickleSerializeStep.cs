using DPorch.Runtime.Python;
using DPorch.Steps;
using Python.Runtime;

namespace DPorch.Runtime.Steps;

/// <summary>
///     Serializes Python objects to byte arrays using Python's pickle module.
/// </summary>
public class PickleSerializeStep : ISerializeStep
{
    const string Name = nameof(PickleSerializeStep);
    const string SerializeFunctionName = "serialize";

    static readonly string PythonCode = @"
import pickle

def serialize(obj):
    """"""Serialize a Python object to bytes using pickle.

    Args:
        obj: Any - Python object to serialize

    Returns:
        bytes - Pickled byte representation of the object
    """"""
    return pickle.dumps(obj)";


    /// <inheritdoc />
    public CancellationToken? StepCancellationToken { get; set; }

    /// <inheritdoc />
    public void Awake()
    {
        PythonGil.AddModule(Name, PythonCode, null);
    }

    /// <inheritdoc />
    public byte[]? Serialize(object? obj)
    {
        using var gil = PythonGil.Get(nameof(Serialize));

        // If data is a PyObject from PythonCodeModule, pass it to serialize
        if (obj is PyObject pyObj)
        {
            using var result = PythonGil.CallFunction(Name, SerializeFunctionName, pyObj);

            return result.As<byte[]>();
        }

        // If no data, pass None
        if (obj == null)
        {
            using var result = PythonGil.CallFunction(Name, SerializeFunctionName, PythonGil.None);

            return result.As<byte[]>();
        }

        // Unexpected type
        throw new InvalidOperationException($"Cannot serialize object of type {obj.GetType()}");
    }

    /// <inheritdoc />
    public void End()
    {
    }
}