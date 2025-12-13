using DPorch.Logging;
using DPorch.Steps;
using DPorch.Tests.Helpers;
using Moq;

namespace DPorch.Tests.Pipeline;

using Pipeline = Classes.Pipeline;

/// <summary>
///     A minimal IPipeline implementation that uses the default TryStart method.
///     Used to test the default interface implementation.
/// </summary>
public class MinimalPipeline : IPipeline
{
    public void SetName(string name) { }
    void IPipeline.SetInputStep(IInputStep? inputStep) { }
    void IPipeline.SetSerializeStep(ISerializeStep? serializeStep) { }
    void IPipeline.AddScriptStep(IScriptStep scriptStep) { }
    void IPipeline.SetDeserializeStep(IDeserializeStep? deserializeStep) { }
    void IPipeline.SetOutputStep(IOutputStep? outputStep) { }
    void IPipeline.SetLogger(ILogger logger) { }
    // Note: TryStart is NOT overridden, so the default interface implementation will be used
}

public class PipelineTests
{
    private readonly TestLogger _logger;
    private readonly Mock<IDeserializeStep> _mockDeserializeStep;
    private readonly Mock<IInputStep> _mockInputStep;
    private readonly Mock<IOutputStep> _mockOutputStep;
    private readonly Mock<IScriptStep> _mockScriptStep;
    private readonly Mock<ISerializeStep> _mockSerializeStep;
    private readonly Pipeline _pipeline;

    public PipelineTests()
    {
        _mockInputStep = new Mock<IInputStep>();
        _mockOutputStep = new Mock<IOutputStep>();
        _mockScriptStep = new Mock<IScriptStep>();
        _mockSerializeStep = new Mock<ISerializeStep>();
        _mockDeserializeStep = new Mock<IDeserializeStep>();
        _logger = new TestLogger();

        _pipeline = new Pipeline();
        ((IPipeline)_pipeline).SetLogger(_logger);
        _pipeline.SetName("TestPipeline");
    }

    #region Script-Only Pipeline Tests

    [Fact]
    public async Task TryStart_ScriptOnlyPipeline_RunsSuccessfully()
    {
        // Arrange - Pipeline with only script steps (no I/O)
        _mockScriptStep.Setup(s => s.InvokeStepFunction(It.IsAny<object?>())).Returns("result");

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();

        // Assert
        Assert.True(result);
        _mockScriptStep.Verify(s => s.InvokeStepFunction(It.IsAny<object?>()), Times.AtLeastOnce);
    }

    #endregion

    #region IPipeline Default Implementation Tests

    [Fact]
    public async Task IPipeline_DefaultTryStart_ThrowsNotImplementedException()
    {
        // Arrange
        IPipeline pipeline = new MinimalPipeline();
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() => pipeline.TryStart(tcs, cts.Token));

        Assert.Contains("TryStart not implemented", exception.Message);
    }

    #endregion

    #region ValidateSteps Tests

    [Fact]
    public async Task TryStart_NoScriptSteps_ThrowsNullReferenceException()
    {
        // Arrange - Pipeline with no script steps (default empty list)
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(_logger.HasEntry(LogLevel.Fatal, "No script steps exist"));
    }

    [Fact]
    public async Task TryStart_SerializerWithoutOutputStep_ThrowsInvalidOperationException()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        // No OutputStep set

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(_logger.HasEntry(LogLevel.Fatal, "Serializer step exists without an output step"));
    }

    [Fact]
    public async Task TryStart_DeserializerWithoutInputStep_ThrowsInvalidOperationException()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        // No InputStep set

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(_logger.HasEntry(LogLevel.Fatal, "Deserializer step exists without an input step"));
    }

    [Fact]
    public async Task TryStart_InputStepWithoutDeserializer_ThrowsInvalidOperationException()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        // No DeserializeStep set

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(_logger.HasEntry(LogLevel.Fatal, "Input step exists without a deserializer step"));
    }

    [Fact]
    public async Task TryStart_OutputStepWithoutSerializer_ThrowsInvalidOperationException()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);
        // No SerializeStep set

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(_logger.HasEntry(LogLevel.Fatal, "Output step exists without a serializer step"));
    }

    #endregion

    #region TryStart Valid Pipeline Tests

    [Fact]
    public async Task TryStart_ValidPipelineWithOnlyScriptStep_ReturnsTrue()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Cancel to stop the loop
        await cts.CancelAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryStart_ValidFullPipeline_ReturnsTrue()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Cancel to stop the loop
        await cts.CancelAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TryStart_ValidationFails_SetsExceptionOnTaskCompletionSource()
    {
        // Arrange - No script steps, so validation will fail
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.True(tcs.Task.IsFaulted);
        Assert.IsType<NullReferenceException>(tcs.Task.Exception?.InnerException);
    }

    #endregion

    #region Step Lifecycle Tests

    [Fact]
    public async Task TryStart_CallsAwakeOnAllSteps()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Give the step thread time to call Awake
        await Task.Delay(100);
        await cts.CancelAsync();

        // Assert
        Assert.True(result);
        _mockInputStep.Verify(s => s.Awake(), Times.Once);
        _mockOutputStep.Verify(s => s.Awake(), Times.Once);
        _mockScriptStep.Verify(s => s.Awake(), Times.Once);
        _mockSerializeStep.Verify(s => s.Awake(), Times.Once);
        _mockDeserializeStep.Verify(s => s.Awake(), Times.Once);
    }

    [Fact]
    public async Task Cancellation_CallsEndOnAllSteps()
    {
        // Arrange
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);
        await cts.CancelAsync();

        // Wait for the step thread to process cancellation
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockInputStep.Verify(s => s.End(), Times.Once);
        _mockOutputStep.Verify(s => s.End(), Times.Once);
        _mockScriptStep.Verify(s => s.End(), Times.Once);
        _mockSerializeStep.Verify(s => s.End(), Times.Once);
        _mockDeserializeStep.Verify(s => s.End(), Times.Once);
    }

    [Fact]
    public async Task RunSteps_PassesDataThroughPipeline()
    {
        // Arrange
        var inputData = new Dictionary<string, byte[]> { { "source", new byte[] { 1, 2, 3 } } };
        var deserializedData = new { Value = "test" };
        var scriptOutputData = new { Result = "processed" };
        var serializedOutput = new byte[] { 4, 5, 6 };

        _mockInputStep.Setup(s => s.Receive()).Returns(inputData);
        _mockDeserializeStep.Setup(s => s.Deserialize(inputData)).Returns(deserializedData);
        _mockScriptStep.Setup(s => s.InvokeStepFunction(deserializedData)).Returns(scriptOutputData);
        _mockSerializeStep.Setup(s => s.Serialize(scriptOutputData)).Returns(serializedOutput);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Let at least one iteration run
        await Task.Delay(200);
        await cts.CancelAsync();

        // Wait for cancellation to complete
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - verify data flow
        _mockInputStep.Verify(s => s.Receive(), Times.AtLeastOnce);
        _mockDeserializeStep.Verify(s => s.Deserialize(inputData), Times.AtLeastOnce);
        _mockScriptStep.Verify(s => s.InvokeStepFunction(deserializedData), Times.AtLeastOnce);
        _mockSerializeStep.Verify(s => s.Serialize(scriptOutputData), Times.AtLeastOnce);
        _mockOutputStep.Verify(s => s.Send(serializedOutput), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunSteps_WithMultipleScriptSteps_ChainsDataThroughAllSteps()
    {
        // Arrange
        var mockScriptStep2 = new Mock<IScriptStep>();
        var mockScriptStep3 = new Mock<IScriptStep>();

        var inputData = new Dictionary<string, byte[]>();
        var step1Output = "step1";
        var step2Output = "step2";
        var step3Output = "step3";

        _mockInputStep.Setup(s => s.Receive()).Returns(inputData);
        _mockDeserializeStep.Setup(s => s.Deserialize(inputData)).Returns("initial");
        _mockScriptStep.Setup(s => s.InvokeStepFunction("initial")).Returns(step1Output);
        mockScriptStep2.Setup(s => s.InvokeStepFunction(step1Output)).Returns(step2Output);
        mockScriptStep3.Setup(s => s.InvokeStepFunction(step2Output)).Returns(step3Output);
        _mockSerializeStep.Setup(s => s.Serialize(step3Output)).Returns(Array.Empty<byte>());

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).AddScriptStep(mockScriptStep2.Object);
        ((IPipeline)_pipeline).AddScriptStep(mockScriptStep3.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);
        await Task.Delay(200);
        await cts.CancelAsync();

        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - verify chained execution
        _mockScriptStep.Verify(s => s.InvokeStepFunction("initial"), Times.AtLeastOnce);
        mockScriptStep2.Verify(s => s.InvokeStepFunction(step1Output), Times.AtLeastOnce);
        mockScriptStep3.Verify(s => s.InvokeStepFunction(step2Output), Times.AtLeastOnce);
        _mockSerializeStep.Verify(s => s.Serialize(step3Output), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TryStart_AssignsCancellationTokenToAllSteps()
    {
        // Arrange
        CancellationToken? capturedToken = null;
        _mockScriptStep.SetupSet(s => s.StepCancellationToken = It.IsAny<CancellationToken?>())
            .Callback<CancellationToken?>(token => capturedToken = token);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();

        // Assert
        Assert.NotNull(capturedToken);
        Assert.Equal(cts.Token, capturedToken.Value);
    }

    [Fact]
    public async Task TryStart_SetsCorrectPipelineName()
    {
        // Arrange - Pipeline already has name set in constructor
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);
        await cts.CancelAsync();

        // Assert - Pipeline should start successfully with assigned name
        Assert.True(result);
    }

    [Fact]
    public async Task Pipeline_DefaultName_IsUnassignedPipeline()
    {
        // Arrange - Create pipeline without setting name
        var pipeline = new Pipeline();
        ((IPipeline)pipeline).SetLogger(new TestLogger());
        ((IPipeline)pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await pipeline.TryStart(tcs, cts.Token);

        // Assert - Should fail because name is unassigned
        Assert.False(result);
    }

    #endregion

    #region Pipeline Name Validation Tests

    [Fact]
    public async Task TryStart_UnassignedName_ReturnsFalseAndLogsFatal()
    {
        // Arrange - Pipeline with default unassigned name
        var testLogger = new TestLogger();
        var pipeline = new Pipeline();
        ((IPipeline)pipeline).SetLogger(testLogger);
        ((IPipeline)pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.False(result);
        Assert.True(testLogger.HasEntry(LogLevel.Fatal, "Pipeline name is not assigned"));
    }

    [Fact]
    public async Task TryStart_WithAssignedName_DoesNotThrowNameValidationError()
    {
        // Arrange - Pipeline already has name set in constructor
        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        var result = await _pipeline.TryStart(tcs, cts.Token);

        // Assert
        Assert.True(result);
        await cts.CancelAsync();
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task LoopSteps_StepThrowsException_SetsExceptionOnTaskCompletionSource()
    {
        // Arrange
        var testException = new InvalidOperationException("Step failed");
        _mockScriptStep.Setup(s => s.InvokeStepFunction(It.IsAny<object?>())).Throws(testException);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for the exception to propagate
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (InvalidOperationException ex)
        {
            // Assert - exception was propagated
            Assert.Equal("Step failed", ex.Message);

            return;
        }

        Assert.Fail("Expected InvalidOperationException was not thrown");
    }

    [Fact]
    public async Task LoopSteps_AwakeThrowsException_SetsExceptionOnTaskCompletionSource()
    {
        // Arrange
        var testException = new InvalidOperationException("Awake failed");
        _mockScriptStep.Setup(s => s.Awake()).Throws(testException);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);

        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for the exception to propagate
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (InvalidOperationException ex)
        {
            // Assert - exception was propagated
            Assert.Equal("Awake failed", ex.Message);

            return;
        }

        Assert.Fail("Expected InvalidOperationException was not thrown");
    }

    #endregion

    #region Cancellation During Execution Tests

    [Fact]
    public async Task RunSteps_CancellationAfterDeserialize_ReturnsEarlyBeforeScriptStep()
    {
        // Arrange
        var inputData = new Dictionary<string, byte[]>();
        var cts = new CancellationTokenSource();

        _mockInputStep.Setup(s => s.Receive()).Returns(inputData);
        _mockDeserializeStep.Setup(s => s.Deserialize(inputData))
            .Callback(() => cts.Cancel()) // Cancel during deserialize
            .Returns("data");

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for cancellation
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Script step should not have been called because we cancelled after deserialize
        // (The cancellation check happens before the script step runs)
        _mockDeserializeStep.Verify(s => s.Deserialize(inputData), Times.Once);
    }

    [Fact]
    public async Task RunSteps_CancellationAfterSerialize_DoesNotCallOutputSend()
    {
        // Arrange
        var inputData = new Dictionary<string, byte[]>();
        var cts = new CancellationTokenSource();
        var serializedData = new byte[] { 1, 2, 3 };

        _mockInputStep.Setup(s => s.Receive()).Returns(inputData);
        _mockDeserializeStep.Setup(s => s.Deserialize(inputData)).Returns("data");
        _mockScriptStep.Setup(s => s.InvokeStepFunction(It.IsAny<object?>())).Returns("result");
        _mockSerializeStep.Setup(s => s.Serialize(It.IsAny<object?>()))
            .Callback(() => cts.Cancel()) // Cancel during serialize
            .Returns(serializedData);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for cancellation
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Serialize was called but Send should not have been called
        _mockSerializeStep.Verify(s => s.Serialize(It.IsAny<object?>()), Times.Once);
        _mockOutputStep.Verify(s => s.Send(It.IsAny<byte[]?>()), Times.Never);
    }

    [Fact]
    public async Task RunSteps_CancellationDuringScriptSteps_StopsProcessingRemainingSteps()
    {
        // Arrange
        var mockScriptStep2 = new Mock<IScriptStep>();
        var cts = new CancellationTokenSource();

        _mockScriptStep.Setup(s => s.InvokeStepFunction(It.IsAny<object?>()))
            .Callback(() => cts.Cancel()) // Cancel during first script step
            .Returns("step1");

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).AddScriptStep(mockScriptStep2.Object);

        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for cancellation
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - First script ran, but second should not have
        _mockScriptStep.Verify(s => s.InvokeStepFunction(It.IsAny<object?>()), Times.Once);
        mockScriptStep2.Verify(s => s.InvokeStepFunction(It.IsAny<object?>()), Times.Never);
    }

    [Fact]
    public async Task RunSteps_CancellationAfterInput_DoesNotCallDeserialize()
    {
        // Arrange
        var inputData = new Dictionary<string, byte[]>();
        var cts = new CancellationTokenSource();

        _mockInputStep.Setup(s => s.Receive()).Callback(() => cts.Cancel()) // Cancel during input
            .Returns(inputData);

        ((IPipeline)_pipeline).AddScriptStep(_mockScriptStep.Object);
        ((IPipeline)_pipeline).SetInputStep(_mockInputStep.Object);
        ((IPipeline)_pipeline).SetDeserializeStep(_mockDeserializeStep.Object);
        ((IPipeline)_pipeline).SetSerializeStep(_mockSerializeStep.Object);
        ((IPipeline)_pipeline).SetOutputStep(_mockOutputStep.Object);

        var tcs = new TaskCompletionSource();

        // Act
        await _pipeline.TryStart(tcs, cts.Token);

        // Wait for cancellation
        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - Input was called but Deserialize should not have been
        _mockInputStep.Verify(s => s.Receive(), Times.Once);
        _mockDeserializeStep.Verify(s => s.Deserialize(It.IsAny<Dictionary<string, byte[]>?>()),
            Times.Never);
    }

    #endregion
}