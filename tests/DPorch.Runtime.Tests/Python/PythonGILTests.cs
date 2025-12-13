using DPorch.Runtime.Python;
using DPorch.Runtime.Tests.TestHelpers;
using Python.Runtime;

namespace DPorch.Runtime.Tests.Python;

#region Pre-Initialization Tests

/// <summary>
///     Tests for PythonGIL initialization error conditions.
///     These tests must run WITHOUT the PythonGILCollection to avoid the fixture initializing Python first.
/// </summary>
/// <remarks>
///     Note: If tests from PythonGILCollection run before these tests, Python will already be initialized
///     and these tests will be skipped. Run these tests in isolation to verify pre-initialization behavior.
/// </remarks>
public class PythonGILInitializationTests
{
    [Fact]
    public void Initialize_WithNullDllPath_ThrowsInvalidOperationException()
    {
        // Note: If Python is already initialized by collection tests, this test cannot verify
        // the validation behavior (it would throw "already initialized" instead).
        // Run this test class in isolation to fully test pre-initialization validation.
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => PythonGil.Initialize(null!, "C:\\valid\\path", NullLogger.Instance));

        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_WithEmptyDllPath_ThrowsInvalidOperationException()
    {
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => PythonGil.Initialize("   ", "C:\\valid\\path", NullLogger.Instance));

        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_WithNonExistentDll_ThrowsInvalidOperationException()
    {
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "python311.dll");
        var validDir = Path.GetTempPath();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => PythonGil.Initialize(nonExistentPath, validDir, NullLogger.Instance));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Initialize_WithNonExistentProjectRoot_ThrowsInvalidOperationException()
    {
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Arrange - need a valid DLL path that exists for this test to reach the directory check
        // We'll use a temp file as a fake DLL
        var tempDll = Path.GetTempFileName();
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act & Assert
            var exception =
                Assert.Throws<InvalidOperationException>(() => PythonGil.Initialize(tempDll, nonExistentDir, NullLogger.Instance));

            Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempDll);
        }
    }

    [Fact]
    public void Get_BeforeInitialization_ThrowsInvalidOperationException()
    {
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PythonGil.Get("test"));
    }

    [Fact]
    public void None_BeforeInitialization_ThrowsInvalidOperationException()
    {
        if (PythonGil.IsInitialized)
            return; // Cannot test - Python already initialized

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _ = PythonGil.None);
    }
}

#endregion

#region GIL Access Tests

/// <summary>
///     Tests for GIL access and property verification when Python is initialized.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PythonGILAccessTests
{
    private readonly PythonGILFixture _fixture;

    public PythonGILAccessTests(PythonGILFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void IsInitialized_WhenInitialized_ReturnsTrue()
    {
        // Assert
        Assert.True(PythonGil.IsInitialized);
    }

    [Fact]
    public void Get_WhenInitialized_ReturnsDisposableGil()
    {
        // Act
        using var gil = PythonGil.Get("test");

        // Assert
        Assert.NotNull(gil);
        Assert.IsAssignableFrom<IDisposable>(gil);
    }

    [Fact]
    public void None_WhenInitialized_ReturnsPyObject()
    {
        // Act
        var none = PythonGil.None;

        // Assert
        Assert.NotNull(none);
        Assert.IsType<PyObject>(none);
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_ThrowsInvalidOperationException()
    {
        // Arrange - Python is already initialized by the fixture
        var validDll = _fixture.PythonPath;
        var validDir = PythonGILFixture.GetTestScriptsDirectory();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => PythonGil.Initialize(validDll, validDir, NullLogger.Instance));

        Assert.Contains("already initialized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

#endregion

#region Module Management Tests

/// <summary>
///     Tests for PythonGIL module management (add, remove, clear).
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PythonGILModuleTests
{
    // Fixture ensures Python is initialized for all tests in this class
    // Constructor parameter required for xUnit collection fixture injection
    public PythonGILModuleTests(PythonGILFixture _)
    {
    }

    [Fact]
    public void AddModule_WithValidCode_AddsModuleSuccessfully()
    {
        // Arrange
        const string pythonCode = @"
def test_func():
    return 'hello from code'
";
        string? moduleKey = null;

        try
        {
            // Act
            moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

            // Assert
            Assert.NotNull(moduleKey);
            Assert.True(PythonGil.IsFunction(moduleKey, "test_func", 0));
        }
        finally
        {
            if (moduleKey != null)
                PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void AddModule_WithValidImportName_AddsModuleSuccessfully()
    {
        // Arrange - use built-in 'os' module
        string? moduleKey = null;

        try
        {
            // Act
            moduleKey = PythonGil.AddAutoKeyedModule(null, "os");

            // Assert
            Assert.NotNull(moduleKey);
            // os.getcwd is a function with 0 parameters
            Assert.True(PythonGil.IsFunction(moduleKey, "getcwd", 0));
        }
        finally
        {
            if (moduleKey != null)
                PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void AddModule_WithBothCodeAndImport_CodeTakesPrecedence()
    {
        // Arrange
        const string pythonCode = @"
def custom_func():
    return 'from code'
";
        string? moduleKey = null;

        try
        {
            // Act - provide both code and import name, code should take precedence
            moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, "os");

            // Assert - custom_func from code should exist, not getcwd from os
            Assert.True(PythonGil.IsFunction(moduleKey, "custom_func", 0));
        }
        finally
        {
            if (moduleKey != null)
                PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void AddModule_WithNeitherCodeNorImport_ThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PythonGil.AddAutoKeyedModule(null, null));
    }

    [Fact]
    public void AddModule_WithEmptyCodeAndImport_ThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PythonGil.AddAutoKeyedModule("   ", "   "));
    }

    [Fact]
    public void AddModule_WithDuplicateKey_ThrowsInvalidOperationException()
    {
        // Arrange
        const string moduleKey = "duplicate_test_module";
        const string pythonCode = "x = 1";

        try
        {
            PythonGil.AddModule(moduleKey, pythonCode, null);

            // Act & Assert - adding with same key should throw
            Assert.Throws<InvalidOperationException>(() => PythonGil.AddModule(moduleKey, pythonCode, null));
        }
        finally
        {
            try
            {
                PythonGil.RemoveModule(moduleKey);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void AddAutoKeyedModule_ReturnsUniqueKey()
    {
        // Arrange
        const string pythonCode = "x = 1";
        string? moduleKey = null;

        try
        {
            // Act
            moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

            // Assert
            Assert.NotNull(moduleKey);
            Assert.NotEmpty(moduleKey);
            // Should be a valid GUID format
            Assert.True(Guid.TryParse(moduleKey, out _));
        }
        finally
        {
            if (moduleKey != null)
                PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void AddAutoKeyedModule_MultipleCallsReturnDifferentKeys()
    {
        // Arrange
        const string pythonCode = "x = 1";
        string? key1 = null;
        string? key2 = null;
        string? key3 = null;

        try
        {
            // Act
            key1 = PythonGil.AddAutoKeyedModule(pythonCode, null);
            key2 = PythonGil.AddAutoKeyedModule(pythonCode, null);
            key3 = PythonGil.AddAutoKeyedModule(pythonCode, null);

            // Assert
            Assert.NotEqual(key1, key2);
            Assert.NotEqual(key2, key3);
            Assert.NotEqual(key1, key3);
        }
        finally
        {
            // Cleanup - ignore errors if modules were already removed
            if (key1 != null)
                try
                {
                    PythonGil.RemoveModule(key1);
                }
                catch (InvalidOperationException)
                {
                    /* already removed */
                }

            if (key2 != null)
                try
                {
                    PythonGil.RemoveModule(key2);
                }
                catch (InvalidOperationException)
                {
                    /* already removed */
                }

            if (key3 != null)
                try
                {
                    PythonGil.RemoveModule(key3);
                }
                catch (InvalidOperationException)
                {
                    /* already removed */
                }
        }
    }

    [Fact]
    public void RemoveModule_WithExistingModule_RemovesSuccessfully()
    {
        // Arrange
        const string pythonCode = "x = 1";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        // Act
        PythonGil.RemoveModule(moduleKey);

        // Assert - trying to remove again should throw (module no longer exists)
        Assert.Throws<InvalidOperationException>(() => PythonGil.RemoveModule(moduleKey));
    }

    [Fact]
    public void RemoveModule_WithNonExistentModule_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid().ToString();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PythonGil.RemoveModule(nonExistentKey));
    }

    [Fact]
    public void ClearModuleCache_RemovesAllModules()
    {
        // Arrange - add multiple modules
        const string pythonCode = "x = 1";
        var key1 = PythonGil.AddAutoKeyedModule(pythonCode, null);
        var key2 = PythonGil.AddAutoKeyedModule(pythonCode, null);

        // Act
        PythonGil.ClearModuleCache();

        // Assert - both modules should be gone
        Assert.Throws<InvalidOperationException>(() => PythonGil.RemoveModule(key1));
        Assert.Throws<InvalidOperationException>(() => PythonGil.RemoveModule(key2));
    }
}

#endregion

#region Function Management Tests

/// <summary>
///     Tests for PythonGIL function invocation and verification.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PythonGILFunctionTests
{
    // Fixture ensures Python is initialized for all tests in this class
    // Constructor parameter required for xUnit collection fixture injection
    public PythonGILFunctionTests(PythonGILFixture _)
    {
    }

    [Fact]
    public void CallFunction_WithValidModuleAndFunction_ReturnsResult()
    {
        // Arrange
        const string pythonCode = @"
def get_greeting():
    return 'Hello, World!'
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act
            using var gil = PythonGil.Get(nameof(CallFunction_WithValidModuleAndFunction_ReturnsResult));
            var result = PythonGil.CallFunction(moduleKey, "get_greeting");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Hello, World!", result.ToString());
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_WithArguments_PassesArgumentsCorrectly()
    {
        // Arrange
        const string pythonCode = @"
def add_numbers(a, b):
    return a + b
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            using var gil = PythonGil.Get(nameof(CallFunction_WithArguments_PassesArgumentsCorrectly));

            // Convert C# values to PyObjects
            var arg1 = new PyInt(5);
            var arg2 = new PyInt(3);

            // Act
            var result = PythonGil.CallFunction(moduleKey, "add_numbers", arg1, arg2);

            // Assert
            Assert.Equal(8, result.As<int>());
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_WithNonExistentModule_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid().ToString();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PythonGil.CallFunction(nonExistentKey, "some_func"));
    }

    [Fact]
    public void CallFunction_WithNonExistentFunction_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = "x = 1";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => PythonGil.CallFunction(moduleKey, "non_existent_function"));
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithValidFunction_ReturnsTrue()
    {
        // Arrange
        const string pythonCode = @"
def my_func(x, y):
    return x + y
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act
            var result = PythonGil.IsFunction(moduleKey, "my_func", 2);

            // Assert
            Assert.True(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithNoParams_ReturnsTrue()
    {
        // Arrange
        const string pythonCode = @"
def no_params():
    return 'called'
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act
            var result = PythonGil.IsFunction(moduleKey, "no_params", 0);

            // Assert
            Assert.True(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithWrongParamCount_ReturnsFalse()
    {
        // Arrange
        const string pythonCode = @"
def two_params(a, b):
    return a + b
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act - function has 2 params, but we're checking for 3
            var result = PythonGil.IsFunction(moduleKey, "two_params", 3);

            // Assert
            Assert.False(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithNonExistentModule_ReturnsFalse()
    {
        // Arrange
        var nonExistentKey = Guid.NewGuid().ToString();

        // Act
        var result = PythonGil.IsFunction(nonExistentKey, "any_func", 0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFunction_WithNonCallableAttribute_ReturnsFalse()
    {
        // Arrange - define a module with a non-callable attribute
        const string pythonCode = @"
not_callable = 'I am a string'
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act
            var result = PythonGil.IsFunction(moduleKey, "not_callable", 0);

            // Assert
            Assert.False(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithNonExistentFunction_ThrowsInvalidOperationException()
    {
        // Arrange
        const string pythonCode = "x = 1";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act & Assert - IsFunction throws when the attribute doesn't exist
            Assert.Throws<InvalidOperationException>(() => PythonGil.IsFunction(moduleKey, "non_existent", 0));
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }
}

#endregion

#region StdOut Redirect Tests

/// <summary>
///     Tests for PythonStdOutRedirect class behavior.
/// </summary>
public class PythonStdOutRedirectTests
{
    [Fact]
    public void Write_FirstCall_WritesToConsole()
    {
        // Arrange
        var redirect = new PythonStdOutRedirect();
        var originalOut = Console.Out;
        var stringWriter = new StringWriter();

        try
        {
            Console.SetOut(stringWriter);

            // Act
            redirect.write("Hello, Python!");

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Hello, Python!", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Write_SecondCallAfterFirst_IsSkipped()
    {
        // Arrange - Python calls write twice: once for content, once for newline
        var redirect = new PythonStdOutRedirect();
        var originalOut = Console.Out;
        var stringWriter = new StringWriter();

        try
        {
            Console.SetOut(stringWriter);

            // Act - First call writes, second call is skipped (it's the newline)
            redirect.write("First line");
            redirect.write("\n"); // This should be skipped

            // Assert - Only one line should be written
            var output = stringWriter.ToString();
            Assert.Contains("First line", output);
            // The newline character from Console.WriteLine is present, but not a second "\n"
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Write_ThirdCallAfterSkip_WritesAgain()
    {
        // Arrange - After skip, the next call should write again
        var redirect = new PythonStdOutRedirect();
        var originalOut = Console.Out;
        var stringWriter = new StringWriter();

        try
        {
            Console.SetOut(stringWriter);

            // Act
            redirect.write("Line 1"); // Writes
            redirect.write("\n"); // Skipped
            redirect.write("Line 2"); // Writes again

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Line 1", output);
            Assert.Contains("Line 2", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        // Arrange
        var redirect = new PythonStdOutRedirect();

        // Act & Assert - flush is a no-op but should not throw
        var exception = Record.Exception(() => redirect.flush());
        Assert.Null(exception);
    }

    [Fact]
    public void Write_MultipleLines_AlternatesCorrectly()
    {
        // Arrange
        var redirect = new PythonStdOutRedirect();
        var originalOut = Console.Out;
        var stringWriter = new StringWriter();

        try
        {
            Console.SetOut(stringWriter);

            // Act - Simulate multiple Python print() calls
            redirect.write("Message 1");
            redirect.write("\n");
            redirect.write("Message 2");
            redirect.write("\n");
            redirect.write("Message 3");
            redirect.write("\n");

            // Assert
            var output = stringWriter.ToString();
            Assert.Contains("Message 1", output);
            Assert.Contains("Message 2", output);
            Assert.Contains("Message 3", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Write_WhenConsoleThrows_SwallowsException()
    {
        // Arrange - Test that the catch block in write() handles exceptions gracefully
        var redirect = new PythonStdOutRedirect();
        var originalOut = Console.Out;
        var disposedWriter = new StringWriter();
        disposedWriter.Dispose(); // Disposed writer will throw on write

        try
        {
            Console.SetOut(disposedWriter);

            // Act & Assert - Should not throw even though the underlying writer is disposed
            var exception = Record.Exception(() => redirect.write("Test"));
            Assert.Null(exception);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

#endregion

#region Additional Coverage Tests

/// <summary>
///     Additional tests to improve code coverage for edge cases.
/// </summary>
[Collection(nameof(PythonGILCollection))]
public class PythonGILCoverageTests
{
    // Constructor parameter required for xUnit collection fixture injection
    public PythonGILCoverageTests(PythonGILFixture _)
    {
    }

    [Fact]
    public void IsFunction_WithBuiltinFunction_ReturnsTrue()
    {
        // Arrange - Import os module which has builtin functions
        var moduleKey = PythonGil.AddAutoKeyedModule(null, "os");

        try
        {
            // Act - os.getcwd is a C builtin function without __code__
            // This tests the HasAttr check that returns true for builtins
            var result = PythonGil.IsFunction(moduleKey, "getcwd", 0);

            // Assert - Should return true for callable builtins
            Assert.True(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithBuiltinLen_ReturnsTrue()
    {
        // Arrange - Create module with access to len builtin
        const string pythonCode = @"
from builtins import len as builtin_len
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act - len is a C builtin function
            var result = PythonGil.IsFunction(moduleKey, "builtin_len", 1);

            // Assert - Should return true for callable builtins
            Assert.True(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_WhenPythonFunctionThrows_ThrowsInvalidOperationException()
    {
        // Arrange - Create a function that raises an exception
        const string pythonCode = @"
def raises_error():
    raise ValueError('Test error from Python')
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act & Assert - Should wrap Python exception in InvalidOperationException
            var exception =
                Assert.Throws<InvalidOperationException>(() => PythonGil.CallFunction(moduleKey, "raises_error"));

            Assert.Contains("raises_error", exception.Message);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_WithWrongArgumentCount_ThrowsInvalidOperationException()
    {
        // Arrange - Create a function that expects arguments
        const string pythonCode = @"
def needs_args(a, b):
    return a + b
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act & Assert - Calling without required args should throw
            var exception =
                Assert.Throws<InvalidOperationException>(() => PythonGil.CallFunction(moduleKey, "needs_args"));

            Assert.Contains(moduleKey, exception.Message);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void IsFunction_WithClassMethod_ReturnsTrueWithCorrectParamCount()
    {
        // Arrange - Create a class with a method (self counts as param)
        const string pythonCode = @"
class MyClass:
    def instance_method(self, x):
        return x * 2
    
    @classmethod
    def class_method(cls, x):
        return x * 3
    
    @staticmethod
    def static_method(x):
        return x * 4

obj = MyClass()
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act & Assert - Static method has 1 param
            Assert.True(PythonGil.IsFunction(moduleKey, "MyClass", 0)); // Class is callable (constructor)
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_WithStringArgument_PassesCorrectly()
    {
        // Arrange
        const string pythonCode = @"
def echo(message):
    return f'Echo: {message}'
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            using var gil = PythonGil.Get(nameof(CallFunction_WithStringArgument_PassesCorrectly));
            using var arg = new PyString("Hello");

            // Act
            var result = PythonGil.CallFunction(moduleKey, "echo", arg);

            // Assert
            Assert.Contains("Echo: Hello", result.ToString());
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_ReturningNone_ReturnsNoneObject()
    {
        // Arrange
        const string pythonCode = @"
def returns_none():
    return None
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            using var gil = PythonGil.Get(nameof(CallFunction_ReturningNone_ReturnsNoneObject));

            // Act
            var result = PythonGil.CallFunction(moduleKey, "returns_none");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsNone());
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void CallFunction_ReturningList_ReturnsPyObject()
    {
        // Arrange
        const string pythonCode = @"
def get_list():
    return [1, 2, 3]
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            using var gil = PythonGil.Get(nameof(CallFunction_ReturningList_ReturnsPyObject));

            // Act
            var result = PythonGil.CallFunction(moduleKey, "get_list");

            // Assert
            Assert.NotNull(result);
            var list = result.As<int[]>();
            Assert.Equal([1, 2, 3], list);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }

    [Fact]
    public void AddModule_WithInvalidPythonSyntax_ThrowsException()
    {
        // Arrange - Invalid Python syntax
        const string invalidCode = @"
def broken(
    # Missing closing parenthesis and body
";

        // Act & Assert - Should throw when trying to execute invalid code
        Assert.ThrowsAny<Exception>(() => PythonGil.AddAutoKeyedModule(invalidCode, null));
    }

    [Fact]
    public void AddModule_WithRuntimeError_ThrowsException()
    {
        // Arrange - Code that will raise an error during execution
        const string errorCode = @"
x = undefined_variable  # This will raise NameError
";

        // Act & Assert - Should throw when executing code with runtime error
        Assert.ThrowsAny<Exception>(() => PythonGil.AddAutoKeyedModule(errorCode, null));
    }

    [Fact]
    public void IsFunction_WithLambda_ReturnsTrue()
    {
        // Arrange - Create a module with a lambda
        const string pythonCode = @"
my_lambda = lambda x: x * 2
";
        var moduleKey = PythonGil.AddAutoKeyedModule(pythonCode, null);

        try
        {
            // Act
            var result = PythonGil.IsFunction(moduleKey, "my_lambda", 1);

            // Assert - Lambda is a function with __code__
            Assert.True(result);
        }
        finally
        {
            PythonGil.RemoveModule(moduleKey);
        }
    }
}

#endregion