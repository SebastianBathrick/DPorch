using DPorch.Logging;

namespace DPorch;

/// <summary>
///     Defines a fluent API for configuring and building pipeline instances. See
///     <see href="../../../docs/ARCHITECTURE.md#builder-pattern">ARCHITECTURE.md</see> for usage details.
/// </summary>
public interface IPipelineBuilder
{
    /// <summary>
    ///     Gets or sets a value indicating whether <see cref="Build" /> has been called.
    /// </summary>
    public bool IsBuilt { get; protected set; }

    /// <summary>
    ///     Gets or initializes the pipeline instance being configured by this builder.
    /// </summary>
    protected IPipeline PipelineInstance { get; init; }


    /// <summary>
    ///     Sets the pipeline identifier used for network discovery and communication. Must be at least 3 characters,
    ///     contain only alphanumerics/hyphens/underscores, and start with a letter.
    /// </summary>
    /// <param name="name">The pipeline identifier.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline name has already been assigned.</exception>
    public IPipelineBuilder SetName(string name);

    /// <summary>
    ///     Sets the logger for pipeline logging operations.
    /// </summary>
    /// <param name="logger">The logger instance to use for pipeline logging.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the logger has already been assigned.</exception>
    public IPipelineBuilder SetLogger(ILogger logger);

    /// <summary>
    ///     Configures the pipeline to receive serialized input from network sources. Must be paired with
    ///     <see cref="SetDeserializeStep" />.
    /// </summary>
    /// <param name="pipelineName">Pipeline identifier for network discovery.</param>
    /// <param name="sourcePipesCount">Number of source pipelines expected to connect.</param>
    /// <param name="discoveryPort">Port number for network discovery.</param>
    /// <param name="inNetIface">Network interface name to receive input on.</param>
    /// <param name="outNetIfaces">Network interface names for sending output.</param>
    /// <returns>This builder instance for method chaining.</returns>
    IPipelineBuilder SetInputStep(string pipelineName,
        int sourcePipesCount,
        int discoveryPort,
        string inNetIface,
        string[] outNetIfaces);

    /// <summary>
    ///     Configures the pipeline to serialize processed data before network transmission. Must be paired with
    ///     <see cref="SetOutputStep" />.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    IPipelineBuilder SetSerializeStep();

    /// <summary>
    ///     Adds a script step to the pipeline's processing chain. Steps execute sequentially in the order added.
    /// </summary>
    /// <param name="name">Identifier for this script step, used for logging.</param>
    /// <param name="code">Script source code to execute (format depends on implementation).</param>
    /// <returns>This builder instance for method chaining.</returns>
    IPipelineBuilder AddScriptStep(string name, string code);

    /// <summary>
    ///     Configures the pipeline to deserialize received input into objects for processing. Must be paired with
    ///     <see cref="SetInputStep" />.
    /// </summary>
    /// <returns>This builder instance for method chaining.</returns>
    IPipelineBuilder SetDeserializeStep();

    /// <summary>
    ///     Configures the pipeline to send serialized output to target pipelines over the network. Must be paired with
    ///     <see cref="SetSerializeStep" />.
    /// </summary>
    /// <param name="pipelineName">Pipeline identifier used as the source name in sent messages.</param>
    /// <param name="outTargStrings">Names of target pipelines to connect to.</param>
    /// <param name="discoveryPort">UDP port number for network discovery.</param>
    /// <param name="inNetIfaces">Network interface names to listen for discovery beacons on.</param>
    /// <returns>This builder instance for method chaining.</returns>
    IPipelineBuilder SetOutputStep(string pipelineName,
        string[] outTargStrings,
        int discoveryPort,
        string[] inNetIfaces);

    /// <summary>
    ///     Returns the configured pipeline instance ready to be started via <see cref="IPipeline.TryStart" />.
    /// </summary>
    IPipeline Build();
}