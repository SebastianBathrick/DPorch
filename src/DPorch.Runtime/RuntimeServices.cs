using DPorch.Classes;
using DPorch.Classes.Logging;
using DPorch.Logging;
using DPorch.Runtime.Python.ManagedVariables;
using DPorch.Runtime.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace DPorch.Runtime;

public class RuntimeServices
{
    public static IServiceProvider GetServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTransient<ILogger, ConsoleLogger>(sp =>
        {
            var log = new ConsoleLogger();
            log.MinimumLogLevel = ILogger.DefaultMinimumLogLevel;

            return log;
        });
        serviceCollection.AddTransient<IPipelineBuilder, PipelineBuilder>(sp =>
        {
            var builder = new PipelineBuilder(new Pipeline(),
                (pipeName, srcPipesCount, discoverPort, inNetIface, outNetIfaces) => new TcpInputStep(pipeName,
                    srcPipesCount, discoverPort, inNetIface, outNetIfaces, sp.GetRequiredService<ILogger>()),
                () => new PickleSerializeStep(),
                (name, code) => new PythonScriptStep(name, code, [new DeltaTimePythonVariable()],
                    sp.GetRequiredService<ILogger>()),
                () => new PickleDeserializeStep(),
                (pipeName, outTargPipes, discoveryPort) => new TcpOutputStep(pipeName, outTargPipes,
                    discoveryPort, sp.GetRequiredService<ILogger>()));

            builder.SetLogger(sp.GetRequiredService<ILogger>());

            return builder;
        });

        return serviceCollection.BuildServiceProvider();
    }
}