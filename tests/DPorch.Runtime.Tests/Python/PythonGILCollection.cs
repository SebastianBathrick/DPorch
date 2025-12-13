namespace DPorch.Runtime.Tests.Python;

/// <summary>
///     xUnit collection definition for tests that require Python GIL access.
/// </summary>
/// <remarks>
///     Apply <c> [Collection(nameof(PythonGILCollection))] </c> to test classes that need Python. This
///     ensures a single <see cref="PythonGILFixture" /> instance is shared and properly disposed.
/// </remarks>
[CollectionDefinition(nameof(PythonGILCollection))]
public class PythonGILCollection : ICollectionFixture<PythonGILFixture>
{
}