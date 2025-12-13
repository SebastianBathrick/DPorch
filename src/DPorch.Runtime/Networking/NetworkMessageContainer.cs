using DPorch.Runtime.Steps;
using DPorch.Steps;

namespace DPorch.Runtime.Networking;

/// <summary>
///     Provides a thread-safe container for managing incoming network messages from multiple source pipelines.
///     Each source pipeline maintains its own message queue, and messages are synchronized to ensure at least
///     one message is available from each source before data retrieval.
/// </summary>
/// <remarks>
///     <para>
///         This class is used by <see cref="TcpInputStep" /> to temporarily store serialized messages
///         from other pipelines until they can be processed. The container maintains a separate queue for
///         each source pipeline identified by its GUID, ensuring messages are preserved in arrival order
///         per source.
///     </para>
///     <para>
///         The synchronization mechanism ensures that <see cref="GetStepMessageMap" /> only returns data
///         when all source pipelines have at least one message available.
///     </para>
///     <para>
///         This class is designed for use with a single producer thread (network receiver) and a single
///         consumer thread (pipeline executor). While individual source queues are thread-safe through
///         internal locking, the container itself assumes single-threaded access patterns for optimal
///         performance.
///     </para>
/// </remarks>
class NetworkMessageContainer
{
    // This dictionary is only accessed by the client from a single thread
    readonly Dictionary<Guid, bool> _isMsgFlags;

    // Dictionary does not need lock, because message containers each have their own locks
    readonly IReadOnlyDictionary<Guid, SourceMessageContainer> _sourceContainerMap;

    /// <summary>
    ///     Creates a message queue for each source pipeline.
    /// </summary>
    /// <remarks>
    ///     Duplicate names are disambiguated with occurrence indices (e.g., "Pipeline (1)", "Pipeline (2)").
    /// </remarks>
    /// <param name="sourceInfoList">
    ///     Source pipeline information containing GUIDs and names.
    /// </param>
    public NetworkMessageContainer(IReadOnlyList<InputSourcePipelineInfo> sourceInfoList)
    {
        var dict = new Dictionary<Guid, SourceMessageContainer>();
        _isMsgFlags = new Dictionary<Guid, bool>();

        // Track name occurrences
        var nameOccurrences = new Dictionary<string, int>();

        foreach (var sourceInfo in sourceInfoList)
        {
            var displayName = sourceInfo.Name;

            // Check if this name has been seen before
            if (nameOccurrences.TryGetValue(sourceInfo.Name, out var count))
            {
                // Append the occurrence index (starting from 1 for the second occurrence)
                displayName = $"{sourceInfo.Name} ({count})";
                nameOccurrences[sourceInfo.Name] = count + 1;
            }
            else
                // First occurrence - use the name as-is
                nameOccurrences[sourceInfo.Name] = 1;

            dict[sourceInfo.Guid] = new SourceMessageContainer(displayName);
            _isMsgFlags.Add(sourceInfo.Guid, false);
        }

        _sourceContainerMap = dict;
    }

    /// <summary>
    ///     Enqueues a serialized message from a specific source pipeline into its dedicated message queue.
    /// </summary>
    /// <remarks>
    ///     This method is thread-safe and can be called by the network receiver thread while the consumer
    ///     thread is processing messages. The message is added to the end of the source's queue and will
    ///     be retrieved in FIFO order.
    /// </remarks>
    /// <param name="sourceGuid">
    ///     The unique identifier of the source pipeline that produced this message.
    /// </param>
    /// <param name="msg">
    ///     The serialized message data as a byte array.
    /// </param>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown if the <paramref name="sourceGuid" /> is not associated with a message queue.
    /// </exception>
    public void Enqueue(Guid sourceGuid, byte[] msg)
    {
        _sourceContainerMap[sourceGuid].Enqueue(msg);
    }

    /// <summary>
    ///     Checks whether at least one message is available from each registered source pipeline.
    /// </summary>
    /// <returns>
    ///     <c> true </c> if every source has at least one message queued; otherwise, <c> false </c>.
    /// </returns>
    /// <remarks>
    ///     May briefly return false during message arrival due to internal caching.
    ///     Use this method to poll for message availability before calling <see cref="GetStepMessageMap" />.
    /// </remarks>
    public bool IsMessageForEachInputSource()
    {
        var isMsgForAll = true;

        // Has potential to return false when true due to race condition, but there are frequent checks
        foreach (var guidContainerPair in _sourceContainerMap)
        {
            // Use a map to avoid unnecessary container lock acquisition
            if (_isMsgFlags[guidContainerPair.Key])
                continue;

            // Even container returns false, the following might not so obtaining their locks + flags early is ideal
            _isMsgFlags[guidContainerPair.Key] = guidContainerPair.Value.IsMessage();
            isMsgForAll = isMsgForAll && _isMsgFlags[guidContainerPair.Key];
        }

        return isMsgForAll;
    }

    /// <summary>
    ///     Retrieves and removes one message from each source pipeline, returning them mapped by source name.
    /// </summary>
    /// <returns>
    ///     Dictionary of source names to message data. Contains exactly one entry per source.
    /// </returns>
    /// <remarks>
    ///     Call <see cref="IsMessageForEachInputSource" /> first to ensure messages are available from
    ///     all sources. Dictionary keys use disambiguated names (e.g., "Pipeline (1)") and match those
    ///     passed to <see cref="IScriptStep" />.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if not every queue in the container has at least one message.
    /// </exception>
    public Dictionary<string, byte[]> GetStepMessageMap()
    {
        if (!IsMessageForEachInputSource())
            throw new InvalidOperationException("Not all source containers have messages available");

        var stepMsgMap = new Dictionary<string, byte[]>();

        foreach (var containerGuidPair in _sourceContainerMap)
        {
            if (!containerGuidPair.Value.TryDequeue(out var msg))
                throw new InvalidOperationException("Message was expected but could not be dequeued");

            stepMsgMap.Add(containerGuidPair.Value.SourceName, msg);
        }

        ResetIsMessageFlags();

        return stepMsgMap;
    }

    void ResetIsMessageFlags()
    {
        foreach (var guid in _sourceContainerMap.Keys)
            _isMsgFlags[guid] = false;
    }

    class SourceMessageContainer(string sourceName)
    {
        readonly object _lock = new();
        readonly Queue<byte[]> _msgQueue = [];

        public string SourceName { get; } = sourceName;

        public bool IsMessage()
        {
            lock (_lock)
                return _msgQueue.Count != 0;
        }

        public bool TryDequeue(out byte[] msg)
        {
            lock (_lock)
                if (_msgQueue.TryDequeue(out var msgDataBuffer))
                {
                    msg = msgDataBuffer;

                    return true;
                }

            msg = [];

            return false;
        }

        public void Enqueue(byte[] msg)
        {
            lock (_lock)
                _msgQueue.Enqueue(msg);
        }
    }
}