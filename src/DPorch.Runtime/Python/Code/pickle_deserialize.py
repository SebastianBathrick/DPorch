import pickle

def deserialize(source_msg_map):
    """Deserialize pickled byte data from each source into a dictionary.

    Args:
        source_msg_map: dict[str, bytes] - Dictionary mapping source names to pickled byte data

    Returns:
        dict[str, Any] - Dictionary mapping source names to deserialized Python objects
    """
    result = {}
    for source, data in source_msg_map.items():
        result[source] = pickle.loads(data)
    return result