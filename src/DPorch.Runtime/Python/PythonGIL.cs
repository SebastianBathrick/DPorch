using System.Collections.Concurrent;
using DPorch.Logging;
using Python.Runtime;
using PythonRuntime = Python.Runtime.Runtime;

namespace DPorch.Runtime.Python;
// TODO: Review PythonGil documentation
/// <summary>
///     Manages the Python.NET runtime with GIL acquisition, module caching, and function invocation.
/// </summary>
/// <remarks>
///     <para>
///         This static class wraps <see cref="PythonEngine" /> and provides thread-safe access to Python
///         modules and functions. All methods automatically acquire the GIL. Usage: initialize runtime,
///         add modules, invoke functions, then shutdown on exit. The cached <see cref="None" /> property
///         provides efficient access to Python's None without repeated GIL acquisition.
///     </para>
/// </remarks>
public static partial class PythonGil
{
    #region Fields

    static PyObject? _noneObj;
    static readonly ConcurrentDictionary<string, PyModule> ModuleCache = new();

    #endregion

    #region Properties

    /// <summary>
    ///     Gets <see cref="PythonEngine.IsInitialized" /> which is true if the Python engine has been initialized.
    /// </summary>
    public static bool IsInitialized => PythonEngine.IsInitialized;

    /// <summary>
    ///     Gets the <see cref="PyObject" /> instance of type None instantiated during
    ///     <see cref="PythonGil.Initialize(string,string,ILogger)" /> call.
    /// </summary>
    /// <remarks>
    ///     This should be used in place of <see cref="PyObject.None" /> to avoid repeated GIL acquisition.
    /// </remarks>
    public static PyObject None => _noneObj ?? throw new InvalidOperationException("Python engine is not initialized");

    static ILogger? Log { get; set; }

    #endregion

    #region GIL Management

    /// <summary>
    ///     Initializes <see cref="PythonEngine" /> using the Python DLL at the provided path.
    /// </summary>
    /// <param name="pythonDllPath">
    ///     Path to the Python DLL used by <see cref="PythonEngine.Initialize()" />.
    /// </param>
    /// <param name="projRootDir">
    ///     Directory that will be set has PYTHONHOME
    /// </param>
    /// <param name="log">
    ///     Logger for providing Python GIL status updates.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the Python engine is already initialized, the DLL path is null/empty or not found, the project root
    /// directory does not exist, or if initialization fails.
    /// </exception>
    public static void Initialize(string pythonDllPath, string projRootDir, ILogger log)
    {
        Log = log;

        Log?.Debug("Attempting to initialize Python engine with {DLL} and project root directory {Dir}", pythonDllPath,
            projRootDir);

        if (PythonEngine.IsInitialized)
            throw new InvalidOperationException("Python engine already initialized");

        if (string.IsNullOrWhiteSpace(pythonDllPath))
            throw new InvalidOperationException("Python DLL path null, whitespace, or empty");

        if (!File.Exists(pythonDllPath))
            throw new InvalidOperationException($"Python DLL not found at path: {pythonDllPath}");

        if (!Directory.Exists(projRootDir))
            throw new InvalidOperationException($"Python project root directory not found {projRootDir}");

        PythonRuntime.PythonDLL = pythonDllPath;

        try
        {
            Log?.Debug("Starting Python engine initialization");
            PythonEngine.Initialize();
            Log?.Debug("Python engine initialized");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Python engine threw exception while initializing", ex);
        }

        using var gil = Get(nameof(Initialize));

        // Cache a None object to avoid creating a new one every time & to avoid locking the GIL
        _noneObj = PyObject.None;

        // Add so user can import modules inside the pipeline root directory and its subdirectories
        dynamic sys = Py.Import(SysModuleName);
        sys.GetAttr(PathAttributeName) .InvokeMethod(AppendMethodName, projRootDir.ToPython()); // TODO: Replace with pythonhome and test

        Log?.Debug("Added pipeline project root directory {ProjectRootDir} to Python's sys.path", projRootDir);

        // Setup StandardOutputRedirect for Python prints to std and error streams
        sys.SetAttr(StdOutAttributeName, new PythonStdOutRedirect().ToPython());
        sys.SetAttr(StdErrAttributeName, new PythonStdOutRedirect().ToPython());

        gil.Dispose();

        // TODO: Test if to check if we can remove this call
        PythonEngine.BeginAllowThreads();
        Log?.Debug("{Method} call completed and now Python modules can be executed", nameof(Initialize));
    }

    /// <summary>
    ///     Shuts down the Python engine and releases all associated resources.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method should be called when the Python engine is no longer needed to ensure
    ///         that all resources are properly disposed. After calling this method, the Python engine
    ///         cannot be restarted within the same process.
    ///     </para>
    ///     <para>
    ///         This method will swallow any exceptions thrown by <see cref="PythonEngine.Shutdown()" /> to avoid
    ///         interruptions while shutting down the application.
    ///     </para>
    /// </remarks>
    public static void Shutdown()
    {
        using var gil = Get(nameof(Shutdown));

        try
        {
            Log?.Debug("Starting Python engine shutdown");
            ClearModuleCache();
            PythonEngine.Shutdown();
            Log?.Debug("Python engine shutdown");
        }
        // It's thrown every time because BinaryFormatter is no longer supported, so ignore it
        catch (PlatformNotSupportedException)
        {
        }
        catch (Exception ex)
        {
            Log?.Warn("Exception thrown on Python engine shutdown.", ex);
        }
    }

    /// <summary>
    ///     Acquires the Python Global Interpreter Lock (GIL) for the current thread.
    /// </summary>
    /// <param name="context">
    ///     The context of the operation that requires the GIL, used for debugging and tracking.
    /// </param>
    /// <returns>
    ///     An <see cref="IDisposable" /> that releases the GIL when disposed. Use with a <c>using</c> statement.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the Python engine has not been initialized via <see cref="Initialize" />.
    /// </exception>
    public static IDisposable Get(string context)
    {
        // TODO: Add short list to add context to so that GIL history can be tracked
        if (!PythonEngine.IsInitialized) throw new InvalidOperationException("Python engine is not initialized");

        return Py.GIL();
    }

    #endregion

    #region Module Management

    /// <summary>
    ///     Creates a distinct Python module with a scope nested under <see cref="Py" />’s global scope,
    ///     then caches the module for repeated use until it is deleted or <see cref="Shutdown" /> is called.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The added module can be referenced later using the specified <paramref name="moduleCacheKey" /> through
    ///         other PythonGIL API calls.
    ///     </para>
    ///     <para>
    ///         If both <paramref name="moduleCode" /> and <paramref name="moduleImportName" /> are provided,
    ///         <paramref name="moduleCode" /> takes precedence and is executed to create the module.
    ///     </para>
    /// </remarks>
    /// <param name="moduleCacheKey">
    ///     The unique cache key that this <see cref="PyModule" /> object will be stored under and
    ///     referenced by in subsequent PythonGIL API calls. This is a C#-side identifier and does
    ///     not need to match the Python module's internal name, import path, or the name Python
    ///     code uses to reference it.
    /// </param>
    /// <param name="moduleCode">
    ///     The Python source code to execute when creating the module. If specified, a new <see cref="PyModule" /> is
    ///     created
    ///     with the <paramref name="moduleCacheKey" /> and this code is
    ///     executed immediately in its scope. Can be null if importing an existing module via
    ///     <paramref
    ///         name="moduleImportName" />
    ///     .
    /// </param>
    /// <param name="moduleImportName">
    ///     The fully qualified import name of an existing Python module to load. If specified and
    ///     <paramref name="moduleCode" /> is null, the module is imported from sys.path using Python's standard import
    ///     mechanism. Can be null if providing <paramref name="moduleCode" />.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <paramref name="moduleCacheKey" /> was already used during a previous AddModule call.
    /// </exception>
    public static void AddModule(string moduleCacheKey, string? moduleCode, string? moduleImportName)
    {
        using var gil = Get(nameof(AddModule));

        Log?.Debug("Adding module {ModuleName}", moduleCacheKey);
        PyModule pyModule;

        if (!string.IsNullOrWhiteSpace(moduleCode))
        {
            // PyModule.FromString creates the module and executes the code immediately
            pyModule = new PyModule(moduleCacheKey);
            pyModule.Exec(moduleCode);
        }
        else if (!string.IsNullOrWhiteSpace(moduleImportName))
            // Import the module directly - this loads the Python file from sys.path
            pyModule = (PyModule)Py.Import(moduleImportName);
        else
            throw new InvalidOperationException($"At least import name or code is required to add module {moduleCacheKey}");

        if (!ModuleCache.TryAdd(moduleCacheKey, pyModule))
            throw new InvalidOperationException($"Module key {moduleCacheKey} already exists in the module cache");

        Log?.Debug("Python module {ModuleName} added", moduleCacheKey);
    }

    /// <remarks>
    ///     <para>
    ///         This method's behavior is the same as <see cref="AddModule(string, string?, string?)" />,
    ///         except that it automatically selects a unique module cache key and returns it after adding
    ///         the module. The added functionality is intended for use in unit tests to avoid collisions
    ///         during varying repeated GIL usage
    ///     </para>
    ///     <para>
    ///         If both <paramref name="moduleCode" /> and <paramref name="moduleImportName" /> are provided,
    ///         <paramref name="moduleCode" /> takes precedence and is executed to create the module.
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     String representing a module cache key used to reference added module through other
    ///     PythonGIL API calls
    /// </returns>
    /// <inheritdoc cref="AddModule(string, string?, string?)" />
    public static string AddAutoKeyedModule(string? moduleCode, string? moduleImportName)
    {
        string moduleCacheKey;

        do
            moduleCacheKey = Guid.NewGuid().ToString();
        while (ModuleCache.ContainsKey(moduleCacheKey));

        AddModule(moduleCacheKey, moduleCode, moduleImportName);

        return moduleCacheKey;
    }

    /// <summary>
    ///     Removes a previously added Python module from the cache and disposes of its resources.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method removes the module from the internal cache and calls Dispose on the <see cref="PyModule" />
    ///         object, releasing its Python resources. The module will no longer be accessible
    ///         through PythonGIL API calls after removal.
    ///     </para>
    ///     <para>
    ///         Note that disposing the PyModule does not necessarily remove the module from Python's
    ///         sys.modules or affect other Python references to it outside of this cache.
    ///     </para>
    /// </remarks>
    /// <param name="moduleCacheKey">
    ///     The unique cache key of the module to remove. This is the same
    ///     <paramref name="moduleCacheKey" /> that was used when the module was added via <see cref="AddModule" />.
    /// </param>
    public static void RemoveModule(string moduleCacheKey)
    {
        using var gil = Get(nameof(RemoveModule));

        ModuleCache.Remove(moduleCacheKey, out var moduleEntry);
        moduleEntry?.Dispose();
        Log?.Debug("Python module {ModuleName} removed", moduleCacheKey);
    }

    /// <summary>
    ///     Clears the module cache and disposes of all associated resources.
    /// </summary>
    /// <remarks>
    ///     This method removes all modules from the internal cache and calls Dispose on the <see cref="PyModule" />
    ///     objects, releasing their Python resources. The modules will no longer be accessible through PythonGIL
    ///     API calls after clearing the cache.
    /// </remarks>
    /// <exception cref="InvalidOperationException" />
    public static void ClearModuleCache()
    {
        using var gil = Get(nameof(ClearModuleCache));
        Log?.Debug("Clearing Python module cache");

        foreach (var module in ModuleCache.Keys)
            RemoveModule(module);

        Log?.Debug("Python module cache cleared");
    }

    static PyModule GetModule(string moduleCacheKey)
    {
        return ModuleCache.TryGetValue(moduleCacheKey, out var module)
            ? module
            : throw new InvalidOperationException($"{moduleCacheKey} is not cached in {nameof(PythonGil)}");
    }

    #endregion

    #region Function Management

    /// <summary>
    ///     Invokes a Python function within a cached module and returns the result.
    /// </summary>
    /// <remarks>
    ///     The function is called within the cached module's scope with the provided arguments. All
    ///     arguments must be valid <see cref="PyObject" /> instances.
    /// </remarks>
    /// <param name="moduleCacheKey">
    ///     The unique cache key of the module to remove. This is the same
    ///     <paramref name="moduleCacheKey" /> that was used when the module was added via <see cref="AddModule" />.
    /// </param>
    /// <param name="funcName"> The name of the Python function to invoke within the module's scope. </param>
    /// <param name="args">
    ///     Zero or more <see cref="PyObject" /> arguments to pass to the function. The number and types must
    ///     match the function's signature.
    /// </param>
    /// <returns> A <see cref="PyObject" /> representing the function's return value. </returns>
    /// <exception cref="InvalidOperationException" />
    public static PyObject CallFunction(string moduleCacheKey, string funcName, params PyObject[] args)
    {
        using var gil = Get(nameof(CallFunction));

        if (!ModuleCache.TryGetValue(moduleCacheKey, out var module))
            throw new InvalidOperationException($"Module not found: {moduleCacheKey}");

        try
        {
            return module.InvokeMethod(funcName, args);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Call to function {funcName} failed using {args.Length} arguments", ex);
        }
    }

    /// <summary>
    ///     Checks whether a Python function with the specified name and parameter count exists in a
    ///     cached module.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method verifies that: (1) the module exists in the cache, (2) the attribute with the
    ///         specified name exists in the module, (3) the attribute is callable, and (4) the function
    ///         accepts exactly the specified number of parameters.
    ///     </para>
    ///     <para>
    ///         This method returns false instead of throwing exceptions when the module or function is not
    ///         found, making it suitable for conditional checks.
    ///     </para>
    /// </remarks>
    /// <param name="moduleCacheKey">
    ///     The unique cache key of the module to remove. This is the same <paramref name="moduleCacheKey" />
    ///     that was used when the module was added via <see cref="AddModule" />.
    /// </param>
    /// <param name="funcName"> The name of the Python function to invoke within the module's scope. </param>
    /// <param name="paramCount">
    ///     The expected number of parameters the function should accept. This
    ///     checks against the function's co_argcount (does not include *args or **kwargs).
    /// </param>
    /// <returns>
    ///     <c> true </c> if the module exists, the function is found, is callable, and has exactly
    ///     <paramref name="paramCount" /> parameters; otherwise, <c> false </c>.
    /// </returns>
    /// <exception cref="InvalidOperationException" />
    public static bool IsFunction(string moduleCacheKey, string funcName, int paramCount)
    {
        using var gil = Get(nameof(IsFunction));

        Log?.Debug("Checking for {ModuleName}.{FunctionName} with {ParamCount} params", moduleCacheKey, funcName,
            paramCount);

        // Return false if module doesn't exist instead of throwing
        if (!ModuleCache.TryGetValue(moduleCacheKey, out var moduleEntry))
            return false;

        try
        {
            if (!moduleEntry.HasAttr(funcName))
                return false;

            // Get the function from the module
            var funcObj = moduleEntry.GetAttr(funcName);

            using dynamic func = funcObj;

            // Check if the attribute is callable
            if (!func.IsCallable())
            {
                Log?.Debug("{ModuleName}.{FunctionName} found but is not callable", moduleCacheKey, funcName);

                return false;
            }

            // Check if this is a Python function (has __code__) vs a C builtin (no __code__)
            // Builtin functions like os.getcwd, len, print don't have __code__ attribute
            if (!func.HasAttr(CodeAttributeName))
            {
                Log?.Debug("{ModuleName}.{FunctionName} is a builtin function (no __code__), cannot verify param count",
                    moduleCacheKey, funcName);

                // Return true for builtins if they're callable - we can't verify param count
                return true;
            }

            // Get the function's code object to check parameter count
            var codeObj = func.GetAttr(CodeAttributeName);

            if (codeObj == null)
            {
                Log?.Debug("{ModuleName}.{FunctionName} found but has no code object", moduleCacheKey, funcName);

                return false;
            }

            using var code = codeObj;
            var actualParamCount = (int)code.GetAttr(ArgCountAttributeName);

            if (actualParamCount == paramCount)
                return true;

            Log?.Debug("{ModuleName}.{FunctionName} has {ActualParamCount}/{ParamCount} params expected",
                moduleCacheKey, funcName, actualParamCount, paramCount);

            return false;
        }
        catch (Exception ex)
        {
            Log?.Warn("Exception thrown while retrieving Python function signature from {Name}: {ex}", funcName, ex);

            return false;
        }
    }

    #endregion

    #region Variable Management

    /// <summary>
    ///     Determines whether a variable exists in the top-level scope of a cached Python module.
    /// </summary>
    /// <param name="moduleCacheKey">
    ///     The unique cache key of the module to check. This is the same <paramref name="moduleCacheKey" />
    ///     that was used when the module was added via <see cref="AddModule" />.  
    /// </param>
    /// <param name="varName">
    ///     The name of the variable to check for existence within the module's global scope.
    /// </param>
    /// <returns>
    ///     <c> true </c> if the variable with the specified name exists in the module's global scope;
    ///     otherwise, <c> false </c>.
    /// </returns>
    public static bool IsGlobalVariable(string moduleCacheKey, string varName)
    {
        using var _ = Get(nameof(IsGlobalVariable));

        return GetModule(moduleCacheKey).Contains(varName);
    }

    /// <summary>
    ///     Sets the value of a global variable in a cached Python module.
    /// </summary>
    /// <param name="moduleCacheKey">
    ///     The unique cache key of the module with the variable. This is the same <paramref name="moduleCacheKey" />
    ///     that was used when the module was added via <see cref="AddModule" />.  
    /// </param>
    /// <param name="varName">
    ///     The name of the global variable that is having its value set.
    /// </param>
    /// <param name="value">
    ///     The <see cref="PyObject" /> value to assign to the global variable.
    /// </param>
    public static void SetGlobalVariableValue(string moduleCacheKey, string varName, PyObject value)
    {
        using var _ = Get(nameof(SetGlobalVariableValue));
        GetModule(moduleCacheKey).Set(varName, value);
    }

    #endregion
}