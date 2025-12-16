import pickle

def serialize(obj):
    """Serialize a Python object to bytes using pickle.

    Args:
        obj: Any - Python object to serialize

    Returns:
        bytes - Pickled byte representation of the object
    """
    return pickle.dumps(obj)