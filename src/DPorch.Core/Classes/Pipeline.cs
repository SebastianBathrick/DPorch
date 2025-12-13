using DPorch.Logging;
using DPorch.Steps;

namespace DPorch.Classes;

/// <summary>
///     Pipeline implementation that executes steps in sequence on an isolated thread.
/// </summary>
public sealed class Pipeline : IPipeline
{
    #region Constants

    const int StepThreadStartTimeoutSec = 5;
    const string StepThreadNameSuffix = " Pipeline Step Thread";
    const string UnassignedPipelineName = "***UNASSIGNED***";

    #endregion

    #region Fields

    ILogger? _log;

    string _name = UnassignedPipelineName;
    IInputStep? _inputStep;
    ISerializeStep? _serializeStep;
    IDeserializeStep? _deserializeStep;
    IOutputStep? _outputStep;
    readonly List<IScriptStep> _scriptSteps = [];

    #endregion
    
    #region Start Methods

    /// <inheritdoc />
    public async Task<bool> TryStart(TaskCompletionSource exitTcs, CancellationToken cancelTkn)
    {
        try
        {
            _log?.Debug("{Name} validating pipeline steps before starting execution thread", _name);
            ValidateSteps();

            // Create TCS the thread will use to signal it has started
            var startTcs = new TaskCompletionSource();

            var stepThread = new Thread(() => RunLifecycle(cancelTkn, startTcs, exitTcs))
            {
                Name = $"{_name} {StepThreadNameSuffix}",
                IsBackground = true
            };

            _log?.Debug("{Name} starting pipeline execution thread", _name);
            stepThread.Start();

            var completedTask = await Task.WhenAny(startTcs.Task,
                Task.Delay(TimeSpan.FromSeconds(StepThreadStartTimeoutSec), cancelTkn));

            if (completedTask != startTcs.Task)
                throw new TimeoutException($"Pipeline execution thread start timeout {StepThreadStartTimeoutSec} seconds");

            _log?.Debug("{Name} pipeline execution thread started successfully", _name);

            return true;
        }
        catch (Exception e)
        {
            _log?.Fatal("{Name} pipeline failed to start: {ErrorMessage}", _name, e.Message);
            exitTcs.SetException(e);

            return false;
        }
    }

    void ValidateSteps()
    {
        if (_name == UnassignedPipelineName)
            throw new InvalidOperationException("Pipeline name is not assigned.");

        if (_serializeStep != null && _outputStep == null)
            throw new InvalidOperationException("Serializer step exists without an output step.");

        if (_deserializeStep != null && _inputStep == null)
            throw new InvalidOperationException("Deserializer step exists without an input step.");

        if (_inputStep != null && _deserializeStep == null)
            throw new InvalidOperationException("Input step exists without a deserializer step.");

        if (_outputStep != null && _serializeStep == null)
            throw new InvalidOperationException("Output step exists without a serializer step.");

        if (!_scriptSteps.Any())
            throw new NullReferenceException("No script steps exist in the pipeline.");
    }

    #endregion

    #region Lifecycle Thread Methods

    void RunLifecycle(CancellationToken cancelTkn, TaskCompletionSource startTcs, TaskCompletionSource exitTcs)
    {
        try
        {
            startTcs.SetResult();
            _log?.Info("{Name} pipeline execution started", _name);

            AssignStepsCancellationToken(cancelTkn);
            AwakenSteps();

            _log?.Info("{Name} pipeline steps awake. Starting steps loop", _name);

            while (!cancelTkn.IsCancellationRequested)
                RunIteration(cancelTkn);
        }
        catch (OperationCanceledException)
        {
            _log?.Debug("{Name} pipeline execution thread detected operation cancellation", _name);
        }
        catch (Exception ex)
        {
            var innerEx = ex;
            
            while(innerEx.InnerException != null)
                innerEx = innerEx.InnerException;

            _log?.Fatal(innerEx, "{Name} pipeline execution thread  encountered error", _name);
            _log?.Debug("Full exception: {Exception}", ex);
            
            exitTcs.SetException(ex);
            return; // Exit without attempting end because this might be an invalid step for end logic
        }

        try
        {
            _log?.Info("{Name} pipeline received cancellation request", _name);
            EndSteps();
            _log?.Info("{Name} pipeline steps ended. Exiting execution thread ", _name);

            // Only set a result if not already completed (e.g., from exception in loop)
            if (!exitTcs.Task.IsCompleted)
                exitTcs.SetResult();
        }
        catch (Exception e)
        {
            _log?.Fatal("{Name} pipeline execution thread  encountered error while ending steps: {ErrorMessage}", _name,
                e.Message);
            _log?.Debug("Full exception: {Exception}", e);
            // Only set exception if not already completed
            if (!exitTcs.Task.IsCompleted)
                exitTcs.SetException(e);

            _log?.Info("Exiting {Name} pipeline execution thread due to unhandled exception", _name);
            throw;
        }
    }

    void AwakenSteps()
    {
        _inputStep?.Awake();
        _deserializeStep?.Awake();
        _scriptSteps.ForEach(s => s.Awake());
        _serializeStep?.Awake();
        _outputStep?.Awake();
    }

    void AssignStepsCancellationToken(CancellationToken cancelTkn)
    {
        _inputStep?.StepCancellationToken = cancelTkn;
        _deserializeStep?.StepCancellationToken = cancelTkn;
        _scriptSteps.ForEach(s => s.StepCancellationToken = cancelTkn);
        _serializeStep?.StepCancellationToken = cancelTkn;
        _outputStep?.StepCancellationToken = cancelTkn;
    }

    void RunIteration(CancellationToken cancelTkn)
    {
        object? processingData = null;
        var serializedInData = _inputStep?.Receive();
        cancelTkn.ThrowIfCancellationRequested();

        processingData = _deserializeStep?.Deserialize(serializedInData);
        cancelTkn.ThrowIfCancellationRequested();

        foreach (var scriptStep in _scriptSteps)
        {
            processingData = scriptStep.InvokeStepFunction(processingData);
            cancelTkn.ThrowIfCancellationRequested();
        }

        var serializedOutData = _serializeStep?.Serialize(processingData);
        cancelTkn.ThrowIfCancellationRequested();

        _outputStep?.Send(serializedOutData);
    }

    void EndSteps()
    {
        _inputStep?.End();
        _deserializeStep?.End();
        foreach (var scriptStep in _scriptSteps)
            scriptStep.End();
        _serializeStep?.End();
        _outputStep?.End();
    }

    #endregion
    
    #region Setter Methods
    
        /// <inheritdoc />
        public void SetName(string name)
        {
            if (_name != UnassignedPipelineName)
                throw new InvalidOperationException("Pipeline name has already been assigned.");
    
            _name = name;
        }
    
        /// <inheritdoc />
        void IPipeline.SetInputStep(IInputStep? inputStep)
        {
            if (_inputStep != null)
                throw new InvalidOperationException("Input step has already been assigned.");
    
            _inputStep = inputStep;
        }
    
        /// <inheritdoc />
        void IPipeline.SetSerializeStep(ISerializeStep? serializeStep)
        {
            if (_serializeStep != null)
                throw new InvalidOperationException("Serialize step has already been assigned.");
    
            _serializeStep = serializeStep;
        }
    
        /// <inheritdoc />
        void IPipeline.AddScriptStep(IScriptStep scriptStep) => _scriptSteps.Add(scriptStep);
    
        /// <inheritdoc />
        void IPipeline.SetDeserializeStep(IDeserializeStep? deserializeStep)
        {
            if (_deserializeStep != null)
                throw new InvalidOperationException("Deserialize step has already been assigned.");
    
            _deserializeStep = deserializeStep;
        }
    
        /// <inheritdoc />
        void IPipeline.SetOutputStep(IOutputStep? outputStep)
        {
            if (_outputStep != null)
                throw new InvalidOperationException("Output step has already been assigned.");

            _outputStep = outputStep;
        }

        /// <inheritdoc />
        void IPipeline.SetLogger(ILogger logger)
        {
            if (_log != null)
                throw new InvalidOperationException("Logger has already been assigned.");

            _log = logger;
        }

        #endregion
}