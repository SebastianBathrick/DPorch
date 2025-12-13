using System.Text;
using DPorch.Runtime.Networking;

namespace DPorch.Runtime.Tests.Networking;

/// <summary>
///     Tests for NetworkMessageContainer message queuing and synchronization.
/// </summary>
public class NetworkMessageContainerTests
{
    #region Thread Safety Tests

    [Fact]
    public async Task Enqueue_ConcurrentEnqueuesFromDifferentSources_AllMessagesQueued()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2),
            new("Pipeline3", guid3)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Act - Enqueue messages concurrently from different threads
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var messageNum = i;
            tasks.Add(Task.Run(() =>
            {
                var msg = Encoding.UTF8.GetBytes($"msg1-{messageNum}");
                container.Enqueue(guid1, msg);
            }));
            tasks.Add(Task.Run(() =>
            {
                var msg = Encoding.UTF8.GetBytes($"msg2-{messageNum}");
                container.Enqueue(guid2, msg);
            }));
            tasks.Add(Task.Run(() =>
            {
                var msg = Encoding.UTF8.GetBytes($"msg3-{messageNum}");
                container.Enqueue(guid3, msg);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have messages from all sources
        Assert.True(container.IsMessageForEachInputSource());

        // Dequeue all messages - should have 100 from each source
        for (var i = 0; i < 100; i++)
        {
            var result = container.GetStepMessageMap();
            Assert.Equal(3, result.Count);
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GetStepMessageMap_AfterGettingMessages_ResetsFlags()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Enqueue and dequeue first set
        container.Enqueue(guid1, "msg1a"u8.ToArray());
        container.Enqueue(guid2, "msg2a"u8.ToArray());
        _ = container.GetStepMessageMap();

        // Act - Check flag is reset
        var hasMessages = container.IsMessageForEachInputSource();

        // Assert
        Assert.False(hasMessages);

        // Enqueue second set and verify it works
        container.Enqueue(guid1, "msg1b"u8.ToArray());
        container.Enqueue(guid2, "msg2b"u8.ToArray());
        Assert.True(container.IsMessageForEachInputSource());
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithSingleSource_CreatesContainer()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", Guid.NewGuid())
        };

        // Act
        var container = new NetworkMessageContainer(sourceInfo);

        // Assert
        Assert.NotNull(container);
        Assert.False(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void Constructor_WithMultipleSources_CreatesContainer()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", Guid.NewGuid()),
            new("Pipeline2", Guid.NewGuid()),
            new("Pipeline3", Guid.NewGuid())
        };

        // Act
        var container = new NetworkMessageContainer(sourceInfo);

        // Assert
        Assert.NotNull(container);
        Assert.False(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void Constructor_WithDuplicateNames_DisambiguatesNames()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline", guid1),
            new("Pipeline", guid2),
            new("Pipeline", guid3)
        };

        var container = new NetworkMessageContainer(sourceInfo);

        // Act - Enqueue messages and verify disambiguated names in result
        container.Enqueue(guid1, "Message1"u8.ToArray());
        container.Enqueue(guid2, "Message2"u8.ToArray());
        container.Enqueue(guid3, "Message3"u8.ToArray());

        var result = container.GetStepMessageMap();

        // Assert - Names should be disambiguated as "Pipeline", "Pipeline (1)", "Pipeline (2)"
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("Pipeline"));
        Assert.True(result.ContainsKey("Pipeline (1)"));
        Assert.True(result.ContainsKey("Pipeline (2)"));
    }

    [Fact]
    public void Constructor_WithEmptyList_CreatesEmptyContainer()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>();

        // Act
        var container = new NetworkMessageContainer(sourceInfo);

        // Assert
        Assert.NotNull(container);
        Assert.True(container.IsMessageForEachInputSource()); // Vacuously true for empty container
    }

    #endregion

    #region Enqueue Tests

    [Fact]
    public void Enqueue_WithValidGuid_AddsMessageToQueue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid)
        };
        var container = new NetworkMessageContainer(sourceInfo);
        var message = "test message"u8.ToArray();

        // Act
        container.Enqueue(guid, message);

        // Assert
        Assert.True(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void Enqueue_WithUnknownGuid_ThrowsKeyNotFoundException()
    {
        // Arrange
        var knownGuid = Guid.NewGuid();
        var unknownGuid = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", knownGuid)
        };
        var container = new NetworkMessageContainer(sourceInfo);
        var message = "test message"u8.ToArray();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => container.Enqueue(unknownGuid, message));
    }

    [Fact]
    public void Enqueue_MultipleMessagesFromSameSource_QueuesAllMessages()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Act - Enqueue multiple messages
        container.Enqueue(guid, "message1"u8.ToArray());
        container.Enqueue(guid, "message2"u8.ToArray());
        container.Enqueue(guid, "message3"u8.ToArray());

        // Assert - All messages should be queued
        Assert.True(container.IsMessageForEachInputSource());
    }

    #endregion

    #region IsMessageForEachInputSource Tests

    [Fact]
    public void IsMessageForEachInputSource_WithNoMessages_ReturnsFalse()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", Guid.NewGuid()),
            new("Pipeline2", Guid.NewGuid())
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Act
        var result = container.IsMessageForEachInputSource();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsMessageForEachInputSource_WithPartialMessages_ReturnsFalse()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Act - Only enqueue message from first source
        container.Enqueue(guid1, "message"u8.ToArray());

        // Assert
        Assert.False(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void IsMessageForEachInputSource_WithAllMessages_ReturnsTrue()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Act - Enqueue messages from all sources
        container.Enqueue(guid1, "message1"u8.ToArray());
        container.Enqueue(guid2, "message2"u8.ToArray());

        // Assert
        Assert.True(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void IsMessageForEachInputSource_AfterDequeue_ReturnsFalse()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        container.Enqueue(guid1, "message1"u8.ToArray());
        container.Enqueue(guid2, "message2"u8.ToArray());

        // Act - Dequeue messages
        _ = container.GetStepMessageMap();

        // Assert
        Assert.False(container.IsMessageForEachInputSource());
    }

    [Fact]
    public void IsMessageForEachInputSource_WithEmptyContainer_ReturnsTrue()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>();
        var container = new NetworkMessageContainer(sourceInfo);

        // Act
        var result = container.IsMessageForEachInputSource();

        // Assert - Vacuously true for empty container (no sources to wait for)
        Assert.True(result);
    }

    #endregion

    #region GetStepMessageMap Tests

    [Fact]
    public void GetStepMessageMap_WithAllMessagesAvailable_ReturnsMessageMap()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        var message1 = "message from pipeline 1"u8.ToArray();
        var message2 = "message from pipeline 2"u8.ToArray();

        container.Enqueue(guid1, message1);
        container.Enqueue(guid2, message2);

        // Act
        var result = container.GetStepMessageMap();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("Pipeline1"));
        Assert.True(result.ContainsKey("Pipeline2"));
        Assert.Equal(message1, result["Pipeline1"]);
        Assert.Equal(message2, result["Pipeline2"]);
    }

    [Fact]
    public void GetStepMessageMap_WithoutAllMessages_ThrowsInvalidOperationException()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid1),
            new("Pipeline2", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Only enqueue message from one source
        container.Enqueue(guid1, "message"u8.ToArray());

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => container.GetStepMessageMap());
        Assert.Contains("source containers have messages", exception.Message);
    }

    [Fact]
    public void GetStepMessageMap_MultipleTimes_DequeuesMessagesInFifoOrder()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline1", guid)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        // Enqueue multiple messages
        var message1 = "first"u8.ToArray();
        var message2 = "second"u8.ToArray();
        var message3 = "third"u8.ToArray();

        container.Enqueue(guid, message1);
        container.Enqueue(guid, message2);
        container.Enqueue(guid, message3);

        // Act - Dequeue in order
        var result1 = container.GetStepMessageMap();
        var result2 = container.GetStepMessageMap();
        var result3 = container.GetStepMessageMap();

        // Assert - Messages should come out in FIFO order
        Assert.Equal(message1, result1["Pipeline1"]);
        Assert.Equal(message2, result2["Pipeline1"]);
        Assert.Equal(message3, result3["Pipeline1"]);
    }

    [Fact]
    public void GetStepMessageMap_WithDisambiguatedNames_UsesCorrectNames()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var sourceInfo = new List<InputSourcePipelineInfo>
        {
            new("Pipeline", guid1),
            new("Pipeline", guid2)
        };
        var container = new NetworkMessageContainer(sourceInfo);

        container.Enqueue(guid1, "msg1"u8.ToArray());
        container.Enqueue(guid2, "msg2"u8.ToArray());

        // Act
        var result = container.GetStepMessageMap();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("Pipeline"));
        Assert.True(result.ContainsKey("Pipeline (1)"));
    }

    [Fact]
    public void GetStepMessageMap_WithEmptyContainer_ReturnsEmptyDictionary()
    {
        // Arrange
        var sourceInfo = new List<InputSourcePipelineInfo>();
        var container = new NetworkMessageContainer(sourceInfo);

        // Act
        var result = container.GetStepMessageMap();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion
}