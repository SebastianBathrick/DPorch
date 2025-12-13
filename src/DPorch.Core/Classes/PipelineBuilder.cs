using DPorch.Logging;
using DPorch.Steps;

namespace DPorch.Classes;

/// <summary>
///     Implements the builder pattern for constructing <see cref="IPipeline" /> instances.
/// </summary>
public sealed class PipelineBuilder(
    IPipeline pipelineObj,
    Func<string, int, int, string, string[], IInputStep> inStepFac,
    Func<ISerializeStep> serialStepFac,
    Func<string, string, IScriptStep> scrStepFac,
    Func<IDeserializeStep> deserialStepFac,
    Func<string, string[], int, IOutputStep> outStepFac) : IPipelineBuilder
{
    public IPipeline PipelineInstance { get; init; } = pipelineObj;
    public bool IsBuilt { get; set; }

    /// <inheritdoc />
    public IPipelineBuilder SetName(string name)
    {
        PipelineInstance.SetName(name);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder SetLogger(ILogger logger)
    {
        PipelineInstance.SetLogger(logger);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder SetInputStep(string pipelineName,
        int sourcePipesCount,
        int discoveryPort,
        string inNetIface,
        string[] outNetIfaces)
    {
        var inputStep = inStepFac(pipelineName, sourcePipesCount, discoveryPort, inNetIface, outNetIfaces);
        PipelineInstance.SetInputStep(inputStep);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder SetSerializeStep()
    {
        var serializeStep = serialStepFac();
        PipelineInstance.SetSerializeStep(serializeStep);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder AddScriptStep(string name, string code)
    {
        var scriptStep = scrStepFac(name, code);
        PipelineInstance.AddScriptStep(scriptStep);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder SetDeserializeStep()
    {
        var deserializeStep = deserialStepFac();
        PipelineInstance.SetDeserializeStep(deserializeStep);

        return this;
    }

    /// <inheritdoc />
    public IPipelineBuilder SetOutputStep(string pipelineName,
        string[] outTargStrings,
        int discoveryPort,
        string[] inNetIfaces)
    {
        var outputStep = outStepFac(pipelineName, outTargStrings, discoveryPort);
        PipelineInstance.SetOutputStep(outputStep);

        return this;
    }

    /// <inheritdoc />
    public IPipeline Build()
    {
        IsBuilt = true;
        return PipelineInstance;
    }
}