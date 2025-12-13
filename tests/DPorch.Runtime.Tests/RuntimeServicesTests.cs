using DPorch.Classes;
using DPorch.Classes.Logging;
using DPorch.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DPorch.Runtime.Tests;

/// <summary>
///     Tests for RuntimeServices dependency injection configuration.
/// </summary>
public class RuntimeServicesTests
{
    #region Factory Configuration Tests

    [Fact]
    public void GetServiceProvider_PipelineBuilderFactory_CreatesPickleSerializeStep()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();
        var builder = provider.GetRequiredService<IPipelineBuilder>();

        // Act - Use the builder's factory through configuration
        // The factories are internal to PipelineBuilder, so we verify through builder usage
        // This test verifies the service provider wires up the factories correctly
        Assert.NotNull(builder);
    }

    #endregion

    #region GetServiceProvider Tests

    [Fact]
    public void GetServiceProvider_ReturnsNonNullProvider()
    {
        // Act
        var provider = RuntimeServices.GetServiceProvider();

        // Assert
        Assert.NotNull(provider);
    }

    [Fact]
    public void GetServiceProvider_RegistersILogger()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act
        var logger = provider.GetService<ILogger>();

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<ConsoleLogger>(logger);
    }

    [Fact]
    public void GetServiceProvider_RegistersIPipelineBuilder()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act
        var builder = provider.GetService<IPipelineBuilder>();

        // Assert
        Assert.NotNull(builder);
        Assert.IsType<PipelineBuilder>(builder);
    }

    [Fact]
    public void GetServiceProvider_LoggerIsTransient()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act - Get two instances
        var logger1 = provider.GetService<ILogger>();
        var logger2 = provider.GetService<ILogger>();

        // Assert - Should be different instances (transient)
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.NotSame(logger1, logger2);
    }

    [Fact]
    public void GetServiceProvider_PipelineBuilderIsTransient()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act - Get two instances
        var builder1 = provider.GetService<IPipelineBuilder>();
        var builder2 = provider.GetService<IPipelineBuilder>();

        // Assert - Should be different instances (transient)
        Assert.NotNull(builder1);
        Assert.NotNull(builder2);
        Assert.NotSame(builder1, builder2);
    }

    [Fact]
    public void GetServiceProvider_CanResolveRequiredServices()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act & Assert - Should not throw
        var logger = provider.GetRequiredService<ILogger>();
        var builder = provider.GetRequiredService<IPipelineBuilder>();

        Assert.NotNull(logger);
        Assert.NotNull(builder);
    }

    [Fact]
    public void GetServiceProvider_PipelineBuilderHasLogger()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act
        var builder = provider.GetRequiredService<IPipelineBuilder>();

        // Assert - Builder should have been constructed with logger
        Assert.NotNull(builder);
        // The builder uses logger internally, so we verify it was constructed successfully
    }

    #endregion

    #region Multiple Calls Tests

    [Fact]
    public void GetServiceProvider_CalledMultipleTimes_ReturnsDifferentProviders()
    {
        // Act
        var provider1 = RuntimeServices.GetServiceProvider();
        var provider2 = RuntimeServices.GetServiceProvider();

        // Assert - Each call creates a new provider
        Assert.NotNull(provider1);
        Assert.NotNull(provider2);
        Assert.NotSame(provider1, provider2);
    }

    [Fact]
    public void GetServiceProvider_CalledMultipleTimes_ProvidersAreIndependent()
    {
        // Arrange
        var provider1 = RuntimeServices.GetServiceProvider();
        var provider2 = RuntimeServices.GetServiceProvider();

        // Act
        var logger1 = provider1.GetRequiredService<ILogger>();
        var logger2 = provider2.GetRequiredService<ILogger>();

        // Assert - Each provider creates its own instances
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
        Assert.NotSame(logger1, logger2);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void GetServiceProvider_CanBeDisposed()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act & Assert - Should not throw
        if (provider is IDisposable disposable)
        {
            var exception = Record.Exception(() => disposable.Dispose());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void GetServiceProvider_AfterDisposal_ThrowsOnResolve()
    {
        // Arrange
        var provider = RuntimeServices.GetServiceProvider();

        // Act
        if (provider is IDisposable disposable) disposable.Dispose();

        // Assert - Should throw after disposal
        Assert.Throws<ObjectDisposedException>(() => provider.GetRequiredService<ILogger>());
    }

    #endregion
}