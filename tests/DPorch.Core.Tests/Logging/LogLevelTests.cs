using DPorch.Logging;

namespace DPorch.Tests.Logging;

public class LogLevelTests
{
    [Fact]
    public void LogLevel_TraceHasValue0()
    {
        Assert.Equal(0, (int)LogLevel.Trace);
    }

    [Fact]
    public void LogLevel_DebugHasValue1()
    {
        Assert.Equal(1, (int)LogLevel.Debug);
    }

    [Fact]
    public void LogLevel_InfoHasValue2()
    {
        Assert.Equal(2, (int)LogLevel.Info);
    }

    [Fact]
    public void LogLevel_WarningHasValue4()
    {
        Assert.Equal(4, (int)LogLevel.Warning);
    }

    [Fact]
    public void LogLevel_ErrorHasValue5()
    {
        Assert.Equal(5, (int)LogLevel.Error);
    }

    [Fact]
    public void LogLevel_FatalHasValue6()
    {
        Assert.Equal(6, (int)LogLevel.Fatal);
    }

    [Fact]
    public void LogLevel_NoneHasValue7()
    {
        Assert.Equal(7, (int)LogLevel.None);
    }

    [Theory]
    [InlineData(LogLevel.Trace, LogLevel.Debug)]
    [InlineData(LogLevel.Debug, LogLevel.Info)]
    [InlineData(LogLevel.Info, LogLevel.Warning)]
    [InlineData(LogLevel.Warning, LogLevel.Error)]
    [InlineData(LogLevel.Error, LogLevel.Fatal)]
    [InlineData(LogLevel.Fatal, LogLevel.None)]
    public void LogLevel_OrderingIsCorrect(LogLevel lower, LogLevel higher)
    {
        Assert.True(lower < higher);
    }
}
