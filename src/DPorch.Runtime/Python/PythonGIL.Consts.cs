namespace DPorch.Runtime.Python;

/// <summary>
///     Constants for <see cref="PythonGil" />.
/// </summary>
public static partial class PythonGil
{
    #region Constants

    // Python system module names
    private const string SysModuleName = "sys";
    private const string PathAttributeName = "path";
    private const string AppendMethodName = "append";
    private const string StdOutAttributeName = "stdout";
    private const string StdErrAttributeName = "stderr";

    // Python introspection attributes
    private const string CodeAttributeName = "__code__";
    private const string ArgCountAttributeName = "co_argcount";

    #endregion
}