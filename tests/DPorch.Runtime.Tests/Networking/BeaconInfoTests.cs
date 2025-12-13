using System.Text.Json;
using DPorch.Runtime.Networking;

namespace DPorch.Runtime.Tests.Networking;

/// <summary>
///     Tests for BeaconInfo record struct serialization and equality.
/// </summary>
public class BeaconInfoTests
{
    #region ToString Tests

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var info = new BeaconInfo("TestBeacon", 8080);

        // Act
        var result = info.ToString();

        // Assert
        Assert.Contains("TestBeacon", result);
        Assert.Contains("8080", result);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var info = new BeaconInfo("TestBeacon", 12345);

        // Assert
        Assert.Equal("TestBeacon", info.Name);
        Assert.Equal(12345, info.ListenerPort);
    }

    [Fact]
    public void Constructor_WithEmptyName_CreatesInstance()
    {
        // Arrange & Act
        var info = new BeaconInfo("", 8080);

        // Assert
        Assert.Equal("", info.Name);
        Assert.Equal(8080, info.ListenerPort);
    }

    [Fact]
    public void Constructor_WithZeroPort_CreatesInstance()
    {
        // Arrange & Act
        var info = new BeaconInfo("Beacon", 0);

        // Assert
        Assert.Equal("Beacon", info.Name);
        Assert.Equal(0, info.ListenerPort);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var info1 = new BeaconInfo("Beacon", 8080);
        var info2 = new BeaconInfo("Beacon", 8080);

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
        var info1 = new BeaconInfo("Beacon1", 8080);
        var info2 = new BeaconInfo("Beacon2", 8080);

        // Act & Assert
        Assert.NotEqual(info1, info2);
        Assert.False(info1 == info2);
        Assert.True(info1 != info2);
    }

    [Fact]
    public void Equality_WithDifferentPorts_AreNotEqual()
    {
        // Arrange
        var info1 = new BeaconInfo("Beacon", 8080);
        var info2 = new BeaconInfo("Beacon", 9090);

        // Act & Assert
        Assert.NotEqual(info1, info2);
        Assert.False(info1 == info2);
        Assert.True(info1 != info2);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new BeaconInfo("TestBeacon", 12345);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<BeaconInfo>(json);

        // Assert
        Assert.Equal(original, deserialized);
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.ListenerPort, deserialized.ListenerPort);
    }

    [Fact]
    public void JsonSerialization_WithSpecialCharactersInName_RoundTrips()
    {
        // Arrange
        var original = new BeaconInfo("Test-Beacon_123", 8080);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<BeaconInfo>(json);

        // Assert
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void JsonDeserialization_FromValidJson_CreatesInstance()
    {
        // Arrange
        var json = """{"Name":"MyBeacon","ListenerPort":5000}""";

        // Act
        var info = JsonSerializer.Deserialize<BeaconInfo>(json);

        // Assert
        Assert.Equal("MyBeacon", info.Name);
        Assert.Equal(5000, info.ListenerPort);
    }

    #endregion
}