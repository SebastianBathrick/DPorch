# DPorch Architecture

This document describes the core architectural patterns, threading model, and design decisions in DPorch.

## Table of Contents
- [Core Design Patterns](#core-design-patterns)
- [Pipeline Lifecycle](#pipeline-lifecycle)
- [Step Types and Data Flow](#step-types-and-data-flow)
- [Threading Model](#threading-model)
- [Builder Pattern](#builder-pattern)
- [Network Discovery](#network-discovery)
- [Python Integration](#python-integration)
- [Dependency Injection](#dependency-injection)

## Core Design Patterns

DPorch uses the **Builder pattern** for pipeline construction and a **three-stage lifecycle** for pipeline execution.

### Builder + Factory Pattern

**Related files:**
- [`IPipelineBuilder`](../src/DPorch.Core/IPipelineBuilder.cs) - Builder interface
- [`IPipeline`](../src/DPorch.Core/IPipeline.cs) - Pipeline interface
- [`IStep`](../src/DPorch.Core/Steps/IStep.cs) - Base step interface
- [`PipelineBuilder`](../src/DPorch.Core/Classes/PipelineBuilder.cs) - Builder implementation
- [`RuntimeServices`](../src/DPorch.Runtime/RuntimeServices.cs) - DI configuration

- `IPipelineBuilder` constructs `IPipeline` instances using a fluent API
- Builders are injected with factory delegates for creating concrete `IStep` implementations
- `RuntimeServices` configures the dependency injection container with all factory implementations
- Factory delegates decouple the Core layer from Runtime implementations

## Pipeline Lifecycle

**Related files:**
- [`Pipeline`](../src/DPorch.Core/Classes/Pipeline.cs) - Core pipeline implementation
- [`TcpInputStep`](../src/DPorch.Runtime/Steps/TcpInputStep.cs) - TCP input step
- [`TcpOutputStep`](../src/DPorch.Runtime/Steps/TcpOutputStep.cs) - TCP output step
- [`PythonScriptStep`](../src/DPorch.Runtime/Steps/PythonScriptStep.cs) - Python script step

Each pipeline runs on an isolated thread with three distinct stages:

### 1. Awake Stage
**Purpose:** Initialize resources required for processing.

**What happens:**
- Executes on the isolated pipeline thread (not the client thread)
- Called once before iteration begins
- Performs UDP discovery, binds & connects TCP sockets, and creates Python Modules
- Failures throw exceptions to prevent invalid iteration state

**Examples:**
- `TcpInputStep` binds a socket and performs UDP discovery
- `PythonScriptStep` loads Python modules and validates function signatures
- `TcpOutputStep` discovers target pipelines and connects sockets

### 2. Iterating Stage
**Purpose:** Continuously process data until cancellation is requested.

**What happens:**
- Runs in a continuous loop checking the cancellation token
- Each iteration executes steps sequentially (see [Data Flow](#data-flow))
- Loop continues until `CancellationToken` is signaled
- Completes the current iteration before moving to End stage

### 3. End Stage
**Purpose:** Clean up resources and any other user-defined end logic before thread termination.

**What happens:**
- Called once after final iteration completes
- Steps release resources (close sockets, dispose objects, terminate threads)
- Exceptions logged but not propagated (thread terminating anyway)

**Examples:**
- `TcpInputStep` joins the background receiver thread
- `PythonScriptStep` optionally calls the user's `end()` function
- `TcpOutputStep` cancels the background sender thread

## Step Types and Data Flow

**Related files:**
- [`IStep`](../src/DPorch.Core/Steps/IStep.cs) - Base step interface
- [`IInputStep`](../src/DPorch.Core/Steps/IInputStep.cs) - Input step interface
- [`IDeserializeStep`](../src/DPorch.Core/Steps/IDeserializeStep.cs) - Deserialize step interface
- [`IScriptStep`](../src/DPorch.Core/Steps/IScriptStep.cs) - Script step interface
- [`ISerializeStep`](../src/DPorch.Core/Steps/ISerializeStep.cs) - Serialize step interface
- [`IOutputStep`](../src/DPorch.Core/Steps/IOutputStep.cs) - Output step interface

All steps implement `IStep` with `Awake()` and `End()` methods. Specialized step interfaces define iteration behavior:

### Step Interface Hierarchy

```
IStep (base interface)
├── IInputStep       - Receives serialized data from network
├── IDeserializeStep - Converts bytes to objects
├── IScriptStep      - Processes data via user scripts
├── ISerializeStep   - Converts objects to bytes
└── IOutputStep      - Sends serialized data to network
```

### Data Flow in Iteration

```
InputStep.Receive()
    ↓ Dictionary<string, byte[]>
DeserializeStep.Deserialize()
    ↓ object?
ScriptStep[0].InvokeStepFunction()
    ↓ object?
ScriptStep[1].InvokeStepFunction()
    ↓ object? (continue for all script steps)
ScriptStep[n].InvokeStepFunction()
    ↓ object?
SerializeStep.Serialize()
    ↓ byte[]?
OutputStep.Send()
```

### Step Configuration Requirements

- **At least one `IScriptStep`** is required (validated in `Pipeline.ValidateSteps()`)
- **Input/Deserialize pairing:** If `InputStep` exists, `DeserializeStep` must exist (and vice versa)
- **Serialize/Output pairing:** If `SerializeStep` exists, `OutputStep` must exist (and vice versa)
- Validation occurs during `IPipeline.TryStart()`, not during `IPipelineBuilder.Build()`

## Threading Model

**Related files:**
- [`Pipeline`](../src/DPorch.Core/Classes/Pipeline.cs) - Thread management and lifecycle

### Pipeline Thread Creation

1. Client calls `IPipeline.TryStart(TaskCompletionSource exitTcs, CancellationToken cancelTkn)`
2. Pipeline validates configuration via `ValidateSteps()`
3. A new background thread is created and started
4. Thread signals startup via internal `TaskCompletionSource`
5. `TryStart()` waits up to 5 seconds for thread startup confirmation
6. Returns `true` if thread started successfully, `false` otherwise

### Thread Communication

- **Client → Pipeline:** `CancellationToken` signals when to stop iterating
- **Pipeline → Client:** `TaskCompletionSource` signals when thread has completed
- **Thread Startup:** Internal `TaskCompletionSource` confirms thread started successfully

### Cancellation Behavior

When cancellation is requested:
1. Iteration loop condition (`!cancelTkn.IsCancellationRequested`) becomes false
2. Current iteration completes fully
3. `EndSteps()` is called to clean up all resources
4. `exitTcs.SetResult()` signals completion to client
5. Thread terminates

### Step Thread Safety

- Each step receives the same `CancellationToken` via `StepCancellationToken` property
- Steps check the token during blocking operations (network I/O, queue waits)
- Background threads in steps (e.g., `TcpInputStep` receiver thread) also use the token
- Steps are not thread-safe across multiple pipelines (each pipeline has its own step instances)

## Builder Pattern

**Related files:**
- [`PipelineBuilder`](../src/DPorch.Core/Classes/PipelineBuilder.cs) - Fluent API implementation
- [`IPipelineBuilder`](../src/DPorch.Core/IPipelineBuilder.cs) - Builder interface

### Fluent API Design

```csharp
var pipeline = builder
    .SetName("my_pipeline")
    .SetInputStep("my_pipeline", sourcesCount: 2, discoveryPort: 5000, "Ethernet", ["Wi-Fi"])
    .SetDeserializeStep()
    .AddScriptStep("script1", "def step(x): return x * 2")
    .AddScriptStep("script2", "def step(x): return x + 10")
    .SetSerializeStep()
    .SetOutputStep("my_pipeline", ["target1", "target2"], 5000, ["Ethernet"])
    .Build();
```

### Factory Delegates

`PipelineBuilder` is constructed with factory delegates for each step type:

```csharp
public PipelineBuilder(
    IPipeline pipelineObj,
    Func<string, int, int, string, string[], IInputStep> inStepFac,
    Func<ISerializeStep> serialStepFac,
    Func<string, string, IScriptStep> scrStepFac,
    Func<IDeserializeStep> deserialStepFac,
    Func<string, string[], int, IOutputStep> outStepFac)
```

When a `Set*Step()` or `AddScriptStep()` method is called, the builder invokes the appropriate factory delegate to create the concrete implementation and assigns it to the `IPipeline` instance.


## Network Discovery

**Related files:**
- [`TcpInputStep`](../src/DPorch.Runtime/Steps/TcpInputStep.cs) - Input pipeline discovery
- [`TcpOutputStep`](../src/DPorch.Runtime/Steps/TcpOutputStep.cs) - Output pipeline discovery
- [`UdpBeacon`](../src/DPorch.Runtime/Networking/UdpBeacon.cs) - UDP beacon broadcaster
- [`UdpFinder`](../src/DPorch.Runtime/Networking/UdpFinder.cs) - UDP beacon listener
- [`BeaconInfo`](../src/DPorch.Runtime/Networking/BeaconInfo.cs) - Beacon message format
- [`InputSourcePipelineInfo`](../src/DPorch.Runtime/Networking/InputSourcePipelineInfo.cs) - Source info message

### Discovery Process

Pipelines discover each other on the local network using UDP beacons and TCP connections:

1. **Input Pipeline (Receiver):**
   - Binds a `PullSocket` to a random TCP port
   - Creates a `UdpBeacon` that broadcasts `BeaconInfo(name, port)` via UDP
   - Waits for expected number of source pipelines to connect
   - Receives `InputSourcePipelineInfo(name, guid)` from each source
   - Sends back TCP URI as acknowledgement
   - Stores source GUIDs for message routing

2. **Output Pipeline (Sender):**
   - Creates a `UdpFinder` listening for target pipeline beacons
   - Waits to discover all target pipeline beacons
   - Sends `InputSourcePipelineInfo(name, guid)` to each target
   - Receives TCP URI acknowledgement from each target
   - Connects `PushSocket` to all target TCP URIs

### Message Format

Messages use a two-frame NetMQ format:
- **Frame 0:** Source pipeline GUID (16 bytes) for identification
- **Frame 1:** Serialized data payload (pickle format)

This allows receivers to identify which source sent each message and route it correctly.

### Network Interfaces

Both `TcpInputStep` and `TcpOutputStep` accept network interface names (e.g., "Ethernet", "Wi-Fi"):
- **Input:** Binds TCP socket to the specified interface's IP
- **Output:** Uses specified interfaces for UDP beacon listening

## Python Integration

**Related files:**
- [`PythonScriptStep`](../src/DPorch.Runtime/Steps/PythonScriptStep.cs) - Python script execution
- [`PythonGIL`](../src/DPorch.Runtime/Python/PythonGIL.cs) - GIL management and module operations
- [`IManagedPythonVariable`](../src/DPorch.Runtime/Python/ManagedVariables/IManagedPythonVariable.cs) - Managed variable interface
- [`DeltaTimePythonVariable`](../src/DPorch.Runtime/Python/ManagedVariables/DeltaTimePythonVariable.cs) - Delta time variable implementation

### PythonScriptStep Design

`PythonScriptStep` integrates user Python scripts using Python.NET:

**Module Loading:**
- Each script becomes a Python module with its name as `__name__`
- Top-level code executes once during `Awake()`
- Module state persists across iterations

**Function Requirements:**
- `step()` function is required with 0 or 1 parameters
- `end()` function is optional with 0 parameters
- Parameter count is validated during `Awake()`

**Execution Flow:**
```python
# Awake stage - executed once
x = 0  # Module-level state persists

def step(data):
    global x
    x += 1
    return data * x

# End stage - optional cleanup
def end():
    print(f"Processed {x} iterations")
```

### Managed Variables

Managed variables (e.g., `DeltaTimePythonVariable`) provide C# values to Python:
- Variables implement `IManagedPythonVariable` interface
- Initialized during `Awake()` if defined in the module's global scope
- Updated after each `InvokeStepFunction()` call
- Allow sharing state between C# and Python (e.g., timing information)

### PythonGil Class

`PythonGil` manages the Global Interpreter Lock:
- All Python operations automatically acquire the GIL
- Module cache prevents redundant module creation
- `None` property provides efficient access to Python's None
- Thread-safe for concurrent pipeline execution

**Initialization:**
```csharp
PythonGil.Initialize(pythonDllPath, projRootDir, logger);
```

**Module Operations:**
```csharp
PythonGil.AddModule(moduleName, sourceCode, stdoutRedirect);
PyObject result = PythonGil.CallFunction(moduleName, "step", argument);
```

## Dependency Injection

**Related files:**
- [`RuntimeServices`](../src/DPorch.Runtime/RuntimeServices.cs) - DI container configuration
- [`ConsoleLogger`](../src/DPorch.Core/Classes/Logging/ConsoleLogger.cs) - Console logger implementation
- [`ILogger`](../src/DPorch.Core/Logging/ILogger.cs) - Logger interface
- [`PickleSerializeStep`](../src/DPorch.Runtime/Steps/PickleSerializeStep.cs) - Pickle serialization
- [`PickleDeserializeStep`](../src/DPorch.Runtime/Steps/PickleDeserializeStep.cs) - Pickle deserialization

`RuntimeServices.GetServiceProvider()` configures all dependencies:

```csharp
var serviceCollection = new ServiceCollection();

// Logger configuration
serviceCollection.AddTransient<ILogger, ConsoleLogger>(sp => {
    var log = new ConsoleLogger();
    log.MinimumLogLevel = ILogger.DefaultMinimumLogLevel;
    return log;
});

// Pipeline builder with factory delegates
serviceCollection.AddTransient<IPipelineBuilder, PipelineBuilder>(sp => {
    return new PipelineBuilder(
        new Pipeline(sp.GetRequiredService<ILogger>()) { ScriptSteps = [] },
        (pipeName, srcPipesCount, discoverPort, inNetIface, outNetIfaces) => 
            new TcpInputStep(pipeName, srcPipesCount, discoverPort, inNetIface, outNetIfaces, sp.GetRequiredService<ILogger>()),
        () => new PickleSerializeStep(),
        (name, code) => new PythonScriptStep(name, code, [new DeltaTimePythonVariable()], sp.GetRequiredService<ILogger>()),
        () => new PickleDeserializeStep(),
        (pipeName, outTargPipes, discoveryPort) => 
            new TcpOutputStep(pipeName, outTargPipes, discoveryPort, sp.GetRequiredService<ILogger>())
    );
});

return serviceCollection.BuildServiceProvider();
```

### Adding New Step Types

When implementing new step types:
1. Create interface inheriting from `IStep` in DPorch.Core
2. Create concrete implementation in DPorch.Runtime
3. Add factory delegate parameter to `PipelineBuilder` constructor
4. Add factory creation lambda to `RuntimeServices` configuration
5. Add builder method to `IPipelineBuilder` interface

## Error Handling

### Startup Errors
- Validation failures in `TryStart()` return `false` and set exception on `exitTcs`
- Resource acquisition failures in `Awake()` throw exceptions, preventing iteration
- Thread startup timeout (5 seconds) returns `false`

### Iteration Errors
- Exceptions during iteration trigger immediate cleanup via `EndSteps()`
- Exception is set on `exitTcs` for client handling
- Thread terminates after cleanup completes

### End Stage Errors
- Exceptions in `End()` are logged but allowed to propagate
- Thread is terminating anyway, so exceptions are expected
- Prevents cascading failures during cleanup

---

*Last updated: December 12, 2025*