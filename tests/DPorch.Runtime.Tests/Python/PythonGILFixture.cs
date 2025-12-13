using DPorch.Runtime.Python;
using DPorch.Runtime.Tests.TestHelpers;

namespace DPorch.Runtime.Tests.Python;

/// <summary>
///     xUnit fixture that initializes the Python GIL for tests.
/// </summary>
/// <remarks>
///     This fixture handles the Python runtime lifecycle for tests.
///     Use with <see cref="PythonGILCollection" /> to share across test classes.
/// </remarks>
public class PythonGILFixture : IDisposable
{
    /// <summary>
    ///     Path to the Python DLL. Update this to match your local Python installation.
    /// </summary>
    private const string PythonDllPath = @"C:\Users\Sebastian\AppData\Local\Programs\Python\Python311\python311.dll";

    private const string RelativeScriptDirectory = "Python/PythonGILTestScripts";

    public PythonGILFixture()
    {
        if (!File.Exists(PythonDllPath))
            throw new FileNotFoundException($"To unit test DPorch.Runtime, you must have Python 3.11 installed. " +
                                            $"Update the PythonDllPath constant in {nameof(PythonGILFixture)} to point to your python311.dll. "
                                            +
                                            $"Expected path: {PythonDllPath}");

        var scriptsDir = GetTestScriptsDirectory();

        // Create scripts directory if it doesn't exist
        if (!Directory.Exists(scriptsDir)) Directory.CreateDirectory(scriptsDir);

        PythonGil.Initialize(PythonDllPath, scriptsDir, NullLogger.Instance);
    }

    /// <summary>
    ///     Gets the path to the Python DLL.
    /// </summary>
    public string PythonPath => PythonDllPath;

    /// <summary>
    ///     Shuts down the Python GIL if it was initialized.
    /// </summary>
    public void Dispose()
    {
        if (PythonGil.IsInitialized) PythonGil.Shutdown();
    }

    /// <summary>
    ///     Gets the directory path where test Python scripts should be placed.
    /// </summary>
    public static string GetTestScriptsDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var scrDir = Path.Combine(baseDir, RelativeScriptDirectory);

        return scrDir;
    }
}