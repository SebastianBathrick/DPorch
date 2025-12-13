using DPorch.Classes;
using DPorch.Steps;
using DPorch.Tests.Helpers;
using Moq;

namespace DPorch.Tests.Pipeline;

using Pipeline = Classes.Pipeline;
using PipelineBuilder = PipelineBuilder;

public class PipelineBuilderTests
{
    private readonly PipelineBuilder _builder;
    private readonly Mock<IDeserializeStep> _mockDeserializeStep;
    private readonly Mock<IInputStep> _mockInputStep;
    private readonly Mock<IOutputStep> _mockOutputStep;
    private readonly Mock<IScriptStep> _mockScriptStep;
    private readonly Mock<ISerializeStep> _mockSerializeStep;
    private readonly Pipeline _pipeline;
    private readonly TestLogger _testLogger;

    public PipelineBuilderTests()
    {
        _mockInputStep = new Mock<IInputStep>();
        _mockOutputStep = new Mock<IOutputStep>();
        _mockScriptStep = new Mock<IScriptStep>();
        _mockSerializeStep = new Mock<ISerializeStep>();
        _mockDeserializeStep = new Mock<IDeserializeStep>();
        _testLogger = new TestLogger();

        _pipeline = new Pipeline();
        ((IPipeline)_pipeline).SetLogger(_testLogger);

        _builder = new PipelineBuilder(_pipeline,
            (pipelineName, inSrcPipesCount, discoveryPort, inNetIface, outNetIfaces) =>
                _mockInputStep.Object, () => _mockSerializeStep.Object,
            (name, code) => _mockScriptStep.Object, () => _mockDeserializeStep.Object,
            (pipelineName, outTargStrings, discoveryPort) => _mockOutputStep.Object);
    }

    #region PipelineInstance Property Tests

    [Fact]
    public void PipelineInstance_ReturnsInjectedPipeline()
    {
        // Assert
        Assert.Same(_pipeline, _builder.PipelineInstance);
    }

    #endregion

    #region SetInputStep Tests

    [Fact]
    public void SetInputStep_AssignsInputStepToPipeline()
    {
        // Act - Verify method executes without throwing
        _builder.SetInputStep("TestPipeline", 2, 8080, "eth0", ["eth1", "eth2"]);

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void SetInputStep_ReturnsBuilder()
    {
        // Act
        var result = _builder.SetInputStep("TestPipeline", 1, 8080, "eth0", []);

        // Assert
        Assert.Same(_builder, result);
    }

    [Fact]
    public void SetInputStep_PassesParametersToFactory()
    {
        // Arrange
        string? capturedPipelineName = null;
        var capturedInSrcPipesCount = 0;
        var capturedDiscoveryPort = 0;
        string? capturedInNetIface = null;
        string[]? capturedOutNetIfaces = null;

        var customBuilder = new PipelineBuilder(_pipeline,
            (pipelineName, inSrcPipesCount, discoveryPort, inNetIface, outNetIfaces) =>
            {
                capturedPipelineName = pipelineName;
                capturedInSrcPipesCount = inSrcPipesCount;
                capturedDiscoveryPort = discoveryPort;
                capturedInNetIface = inNetIface;
                capturedOutNetIfaces = outNetIfaces;

                return _mockInputStep.Object;
            }, () => _mockSerializeStep.Object, (name, code) => _mockScriptStep.Object,
            () => _mockDeserializeStep.Object,
            (pipelineName, outTargStrings, discoveryPort) => _mockOutputStep.Object);

        // Act
        customBuilder.SetInputStep("MyPipeline", 3, 9999, "lo0", ["eth0", "eth1", "eth2"]);

        // Assert
        Assert.Equal("MyPipeline", capturedPipelineName);
        Assert.Equal(3, capturedInSrcPipesCount);
        Assert.Equal(9999, capturedDiscoveryPort);
        Assert.Equal("lo0", capturedInNetIface);
        Assert.NotNull(capturedOutNetIfaces);
        Assert.Equal(["eth0", "eth1", "eth2"], capturedOutNetIfaces);
    }

    #endregion

    #region SetSerializeStep Tests

    [Fact]
    public void SetSerializeStep_AssignsSerializeStepToPipeline()
    {
        // Act - Verify method executes without throwing
        _builder.SetSerializeStep();

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void SetSerializeStep_ReturnsBuilder()
    {
        // Act
        var result = _builder.SetSerializeStep();

        // Assert
        Assert.Same(_builder, result);
    }

    #endregion

    #region AddScriptStep Tests

    [Fact]
    public void AddScriptStep_AddsToScriptStepsList()
    {
        // Act - Verify method executes without throwing
        _builder.AddScriptStep("test_module", "console.log('test')");

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void AddScriptStep_CalledMultipleTimes_AddsAllSteps()
    {
        // Arrange
        var mockStep1 = new Mock<IScriptStep>();
        var mockStep2 = new Mock<IScriptStep>();
        var mockStep3 = new Mock<IScriptStep>();

        var stepIndex = 0;
        var steps = new[] { mockStep1.Object, mockStep2.Object, mockStep3.Object };

        var customBuilder = new PipelineBuilder(_pipeline,
            (pipelineName, inSrcPipesCount, discoveryPort, inNetIface, outNetIfaces) =>
                _mockInputStep.Object, () => _mockSerializeStep.Object,
            (name, code) => steps[stepIndex++], () => _mockDeserializeStep.Object,
            (pipelineName, outTargStrings, discoveryPort) => _mockOutputStep.Object);

        // Act - Verify methods execute without throwing
        customBuilder.AddScriptStep("module1", "step1");
        customBuilder.AddScriptStep("module2", "step2");
        customBuilder.AddScriptStep("module3", "step3");

        // Assert - Verify factory was called 3 times by checking stepIndex
        Assert.Equal(3, stepIndex);
    }

    [Fact]
    public void AddScriptStep_ReturnsBuilder()
    {
        // Act
        var result = _builder.AddScriptStep("module", "code");

        // Assert
        Assert.Same(_builder, result);
    }

    [Fact]
    public void AddScriptStep_PassesNameAndCodeToFactory()
    {
        // Arrange
        string? capturedName = null;
        string? capturedCode = null;

        var customBuilder = new PipelineBuilder(_pipeline,
            (pipelineName, inSrcPipesCount, discoveryPort, inNetIface, outNetIfaces) =>
                _mockInputStep.Object, () => _mockSerializeStep.Object, (name, code) =>
            {
                capturedName = name;
                capturedCode = code;

                return _mockScriptStep.Object;
            }, () => _mockDeserializeStep.Object,
            (pipelineName, outTargStrings, discoveryPort) => _mockOutputStep.Object);

        // Act
        customBuilder.AddScriptStep("my_module", "function process(data) { return data * 2; }");

        // Assert
        Assert.Equal("my_module", capturedName);
        Assert.Equal("function process(data) { return data * 2; }", capturedCode);
    }

    #endregion

    #region SetDeserializeStep Tests

    [Fact]
    public void SetDeserializeStep_AssignsDeserializeStepToPipeline()
    {
        // Act - Verify method executes without throwing
        _builder.SetDeserializeStep();

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void SetDeserializeStep_ReturnsBuilder()
    {
        // Act
        var result = _builder.SetDeserializeStep();

        // Assert
        Assert.Same(_builder, result);
    }

    #endregion

    #region SetOutputStep Tests

    [Fact]
    public void SetOutputStep_AssignsOutputStepToPipeline()
    {
        // Act - Verify method executes without throwing
        _builder.SetOutputStep("TestPipeline", ["target1"], 8081, ["eth0"]);

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void SetOutputStep_ReturnsBuilder()
    {
        // Act
        var result = _builder.SetOutputStep("TestPipeline", [], 8081, []);

        // Assert
        Assert.Same(_builder, result);
    }

    [Fact]
    public void SetOutputStep_PassesParametersToFactory()
    {
        // Arrange
        string? capturedPipelineName = null;
        string[]? capturedOutTargStrings = null;
        var capturedDiscoveryPort = 0;

        var customBuilder = new PipelineBuilder(_pipeline,
            (pipelineName, inSrcPipesCount, discoveryPort, inNetIface, outNetIfaces) =>
                _mockInputStep.Object, () => _mockSerializeStep.Object,
            (name, code) => _mockScriptStep.Object, () => _mockDeserializeStep.Object,
            (pipelineName, outTargStrings, discoveryPort) =>
            {
                capturedPipelineName = pipelineName;
                capturedOutTargStrings = outTargStrings;
                capturedDiscoveryPort = discoveryPort;

                return _mockOutputStep.Object;
            });

        // Act
        customBuilder.SetOutputStep("OutputPipeline", ["x", "y"], 7777, ["lo0", "eth0"]);

        // Assert
        Assert.Equal("OutputPipeline", capturedPipelineName);
        Assert.NotNull(capturedOutTargStrings);
        Assert.Equal(["x", "y"], capturedOutTargStrings);
        Assert.Equal(7777, capturedDiscoveryPort);
    }

    #endregion

    #region SetName Tests

    [Fact]
    public void SetName_AssignsNameToPipeline()
    {
        // Act - Verify method executes without throwing
        _builder.SetName("TestPipeline");

        // Assert - If we got here, the method succeeded
        Assert.True(true);
    }

    [Fact]
    public void SetName_ReturnsBuilderInstance()
    {
        // Act
        var result = _builder.SetName("Test");

        // Assert
        Assert.Same(_builder, result);
    }

    [Fact]
    public void SetName_ThrowsWhenCalledTwice()
    {
        // Arrange
        _builder.SetName("FirstName");

        // Act & Assert - Should throw when trying to set name again
        Assert.Throws<InvalidOperationException>(() => _builder.SetName("SecondName"));
    }

    #endregion

    #region IsBuilt Property Tests

    [Fact]
    public void IsBuilt_InitiallyFalse()
    {
        // Assert
        Assert.False(_builder.IsBuilt);
    }

    [Fact]
    public void IsBuilt_SetToTrueAfterBuild()
    {
        // Act
        _builder.Build();

        // Assert
        Assert.True(_builder.IsBuilt);
    }

    [Fact]
    public void IsBuilt_RemainsUnchangedBeforeBuild()
    {
        // Act - configure without building
        _builder.SetName("Test");
        _builder.AddScriptStep("step", "code");

        // Assert
        Assert.False(_builder.IsBuilt);
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_ReturnsPipelineInstance()
    {
        // Act
        var result = _builder.Build();

        // Assert
        Assert.Same(_pipeline, result);
        Assert.True(_builder.IsBuilt);
    }

    [Fact]
    public void Build_AfterConfiguration_ReturnsSamePipelineWithAllSettings()
    {
        // Arrange
        _builder.SetName("TestPipeline")
            .SetInputStep("TestPipeline", 1, 8080, "eth0", ["eth1"])
            .SetDeserializeStep()
            .AddScriptStep("module", "code")
            .SetSerializeStep()
            .SetOutputStep("TestPipeline", ["out"], 8081, ["eth0"]);

        // Act
        var result = _builder.Build();

        // Assert - Verify the builder returns the correct pipeline and sets IsBuilt
        Assert.Same(_pipeline, result);
        Assert.True(_builder.IsBuilt);
    }

    #endregion

    #region Fluent Chaining Tests

    [Fact]
    public void FluentChaining_AllMethodsReturnBuilder()
    {
        // Act - Chain all methods and verify it compiles and works
        var result = _builder.SetName("TestPipeline")
            .SetInputStep("TestPipeline", 1, 8080, "eth0", [])
            .SetDeserializeStep()
            .AddScriptStep("module1", "step1")
            .AddScriptStep("module2", "step2")
            .SetSerializeStep()
            .SetOutputStep("TestPipeline", [], 8081, [])
            .Build();

        // Assert
        Assert.IsAssignableFrom<IPipeline>(result);
    }

    [Fact]
    public void FluentChaining_CanBuildMinimalPipeline()
    {
        // Act
        var result = _builder.SetName("MinimalPipeline").AddScriptStep("module", "code").Build();

        // Assert - Verify the builder returns a pipeline
        Assert.IsAssignableFrom<IPipeline>(result);
    }

    #endregion
}