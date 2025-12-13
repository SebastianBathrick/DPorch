using System.Text.Json;
using DPorch.Runtime.Networking;

namespace DPorch.Runtime.Tests.Networking;

/// <summary>
///     Tests for InputSourcePipelineInfo record struct serialization and equality.
/// </summary>
public class InputSourcePipelineInfoTests
{
    #region ToString Tests

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var info = new InputSourcePipelineInfo("TestPipeline", guid);

        // Act
        var result = info.ToString();

        // Assert
        Assert.Contains("TestPipeline", result);
        Assert.Contains(guid.ToString(), result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var info = new InputSourcePipelineInfo("Pipeline1", guid);

        // Assert
        Assert.Equal("Pipeline1", info.Name);
        Assert.Equal(guid, info.Guid);
    }

    [Fact]
    public void Constructor_WithEmptyName_CreatesInstance()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var info = new InputSourcePipelineInfo("", guid);

        // Assert
        Assert.Equal("", info.Name);
        Assert.Equal(guid, info.Guid);
    }

    [Fact]
    public void Constructor_WithEmptyGuid_CreatesInstance()
    {
        // Arrange & Act
        var info = new InputSourcePipelineInfo("Pipeline", Guid.Empty);

        // Assert
        Assert.Equal("Pipeline", info.Name);
        Assert.Equal(Guid.Empty, info.Guid);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var info1 = new InputSourcePipelineInfo("Pipeline", guid);
        var info2 = new InputSourcePipelineInfo("Pipeline", guid);

        // Act & Assert
        Assert.Equal(info1, info2);
        Assert.True(info1 == info2);
        Assert.False(info1 != info2);
        Assert.Equal(info1.GetHashCode(), info2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentNames_AreNotEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var info1 = new InputSourcePipelineInfo("Pipeline1", guid);
        var info2 = new InputSourcePipelineInfo("Pipeline2", guid);

        // Act & Assert
        Assert.NotEqual(info1, info2);
        Assert.False(info1 == info2);
        Assert.True(info1 != info2);
    }

    [Fact]
    public void Equality_WithDifferentGuids_AreNotEqual()
    {
        // Arrange
        var info1 = new InputSourcePipelineInfo("Pipeline", Guid.NewGuid());
        var info2 = new InputSourcePipelineInfo("Pipeline", Guid.NewGuid());

        // Act & Assert
        Assert.NotEqual(info1, info2);
        Assert.False(info1 == info2);
        Assert.True(info1 != info2);
    }

    [Fact]
    public void Equality_WithBothDifferent_AreNotEqual()
    {
        // Arrange
        var info1 = new InputSourcePipelineInfo("Pipeline1", Guid.NewGuid());
        var info2 = new InputSourcePipelineInfo("Pipeline2", Guid.NewGuid());

        // Act & Assert
        Assert.NotEqual(info1, info2);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesData()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var original = new InputSourcePipelineInfo("TestPipeline", guid);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<InputSourcePipelineInfo>(json);

        // Assert
        Assert.Equal(original, deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Guid, deserialized.Guid);
    }

    [Fact]
    public void JsonSerialization_WithSpecialCharactersInName_RoundTrips()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var original = new InputSourcePipelineInfo("Test-Pipeline_123", guid);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<InputSourcePipelineInfo>(json);

        // Assert
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void JsonDeserialization_FromValidJson_CreatesInstance()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var json = $$"""{"Name":"MyPipeline","Guid":"{{guid}}"}""";

        // Act
        var info = JsonSerializer.Deserialize<InputSourcePipelineInfo>(json);

        // Assert
        Assert.Equal("MyPipeline", info.Name);
        Assert.Equal(guid, info.Guid);
    }

    [Fact]
    public void JsonSerialization_WithEmptyGuid_RoundTrips()
    {
        // Arrange
        var original = new InputSourcePipelineInfo("Pipeline", Guid.Empty);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<InputSourcePipelineInfo>(json);

        // Assert
        Assert.Equal(original, deserialized);
        Assert.Equal(Guid.Empty, deserialized.Guid);
    }

    #endregion
}