using DPorch.Classes.Logging;
using DPorch.Logging;

namespace DPorch.Tests.Logging;

public class ConsoleLoggerTests : IDisposable
{
    private readonly ConsoleLogger _logger;
    private readonly TextWriter _originalOutput;
    private readonly StringWriter _stringWriter;

    public ConsoleLoggerTests()
    {
        _originalOutput = Console.Out;
        _stringWriter = new StringWriter();
        Console.SetOut(_stringWriter);
        _logger = new ConsoleLogger();
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _stringWriter.Dispose();
    }

    private string GetConsoleOutput()
    {
        _stringWriter.Flush();

        return _stringWriter.ToString();
    }

    private void ClearConsoleOutput()
    {
        _stringWriter.GetStringBuilder().Clear();
    }

    #region Write Method Tests

    [Fact]
    public void Write_WithProperties_FormatsMessageCorrectly()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "User {Username} logged in from {IP}", "john", "192.168.1.1");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Info]", output);
        Assert.Contains("User", output);
        Assert.Contains("john", output);
        Assert.Contains("logged in from", output);
        Assert.Contains("192.168.1.1", output);
    }

    [Fact]
    public void Write_BelowMinimumLevel_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Warning;

        // Act
        _logger.Write(LogLevel.Info, "This should not appear");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    [Fact]
    public void Write_AtMinimumLevel_DoesOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Warning;

        // Act
        _logger.Write(LogLevel.Warning, "This should appear");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Warning]", output);
        Assert.Contains("This should appear", output);
    }

    [Fact]
    public void Write_AboveMinimumLevel_DoesOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Warning;

        // Act
        _logger.Write(LogLevel.Error, "This should appear");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Error]", output);
    }

    [Fact]
    public void Write_WithException_IncludesExceptionDetails()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Error;
        var exception = new InvalidOperationException("Test exception message");

        // Act
        _logger.Write(LogLevel.Error, "An error occurred: {Error}", exception, "details");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Error]", output);
        Assert.Contains("An error occurred:", output);
        Assert.Contains("details", output);
        Assert.Contains("Exception: InvalidOperationException", output);
        Assert.Contains("Test exception message", output);
    }

    [Fact]
    public void Write_WithException_BelowMinimumLevel_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Fatal;
        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.Write(LogLevel.Error, "Error with exception", exception);

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Info)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Fatal)]
    public void Write_AllLogLevels_FormatsCorrectly(LogLevel level)
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Trace;

        // Act
        _logger.Write(level, "Test message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains($"[{level}]", output);
        Assert.Contains("Test message", output);
    }

    [Fact]
    public void Write_WithNoProperties_OutputsMessageAsIs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "Simple message without placeholders");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("Simple message without placeholders", output);
    }

    [Fact]
    public void Write_WithNullProperties_HandlesGracefully()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "Message with null", (object?[]?)null);

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("Message with null", output);
    }

    [Fact]
    public void Write_WithMorePlaceholdersThanProps_LeavesExtraPlaceholdersEmpty()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "{First} {Second} {Third}", "one", "two");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("one", output);
        Assert.Contains("two", output);
    }

    [Fact]
    public void Write_WithUnmatchedOpenBrace_AppendsRemainder()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "Start {incomplete");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("Start {incomplete", output);
    }

    [Fact]
    public void Write_WithNullPropertyValue_InsertsEmptyString()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Write(LogLevel.Info, "Value is: {Value}", (object?)null);

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("Value is: ", output);
    }

    #endregion

    #region Info Method Tests

    [Fact]
    public void Info_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Info("Test message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Info]", output);
        Assert.Contains("Test message", output);
    }

    [Fact]
    public void Info_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Warning;

        // Act
        _logger.Info("Test message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    [Fact]
    public void Info_WithProperties_FormatsMessage()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Info("User {Name} connected", "Alice");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("User", output);
        Assert.Contains("Alice", output);
        Assert.Contains("connected", output);
    }

    #endregion

    #region Warn Method Tests

    [Fact]
    public void Warn_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Warning;

        // Act
        _logger.Warn("Warning message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Warning]", output);
        Assert.Contains("Warning message", output);
    }

    [Fact]
    public void Warn_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Error;

        // Act
        _logger.Warn("Warning message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    #endregion

    #region Error Method Tests

    [Fact]
    public void Error_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Error;

        // Act
        _logger.Error("Error message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Error]", output);
        Assert.Contains("Error message", output);
    }

    [Fact]
    public void Error_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Fatal;

        // Act
        _logger.Error("Error message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    [Fact]
    public void Error_WithException_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Error;
        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.Error(exception, "Error with exception");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Error]", output);
        Assert.Contains("Error with exception", output);
        Assert.Contains("InvalidOperationException", output);
    }

    [Fact]
    public void Error_WithException_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Fatal;
        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.Error(exception, "Error with exception");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    #endregion

    #region Fatal Method Tests

    [Fact]
    public void Fatal_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Fatal;

        // Act
        _logger.Fatal("Fatal message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Fatal]", output);
        Assert.Contains("Fatal message", output);
    }

    [Fact]
    public void Fatal_WhenLevelIsNone_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.None;

        // Act
        _logger.Fatal("Fatal message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    [Fact]
    public void Fatal_WithException_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Fatal;
        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.Fatal(exception, "Fatal with exception");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Fatal]", output);
        Assert.Contains("Fatal with exception", output);
        Assert.Contains("InvalidOperationException", output);
    }

    [Fact]
    public void Fatal_WithException_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.None;
        var exception = new InvalidOperationException("Test exception");

        // Act
        _logger.Fatal(exception, "Fatal with exception");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    #endregion

    #region Debug Method Tests

    [Fact]
    public void Debug_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Debug;

        // Act
        _logger.Debug("Debug message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Debug]", output);
        Assert.Contains("Debug message", output);
    }

    [Fact]
    public void Debug_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Info;

        // Act
        _logger.Debug("Debug message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    #endregion

    #region Trace Method Tests

    [Fact]
    public void Trace_WhenLevelAllows_Outputs()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Trace;

        // Act
        _logger.Trace("Trace message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Contains("[Trace]", output);
        Assert.Contains("Trace message", output);
    }

    [Fact]
    public void Trace_WhenLevelTooHigh_DoesNotOutput()
    {
        // Arrange
        _logger.MinimumLogLevel = LogLevel.Debug;

        // Act
        _logger.Trace("Trace message");

        // Assert
        var output = GetConsoleOutput();
        Assert.Empty(output);
    }

    #endregion

    #region MinimumLogLevel Tests

    [Fact]
    public void MinimumLogLevel_DefaultIsInfo()
    {
        // Arrange
        var logger = new ConsoleLogger();

        // Assert
        Assert.Equal(LogLevel.Info, logger.MinimumLogLevel);
    }

    [Fact]
    public void MinimumLogLevel_CanBeChanged()
    {
        // Arrange
        Assert.Equal(LogLevel.Info, _logger.MinimumLogLevel);

        // Act
        _logger.MinimumLogLevel = LogLevel.Debug;

        // Assert
        Assert.Equal(LogLevel.Debug, _logger.MinimumLogLevel);
    }

    #endregion
}