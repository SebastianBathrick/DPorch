"""
Test helper functions for PythonGIL unit tests.
These functions are used to verify function invocation and introspection.
"""


def no_params():
    """Function with no parameters."""
    return "no_params_called"


def one_param(x):
    """Function with one parameter."""
    return f"received: {x}"


def two_params(a, b):
    """Function with two parameters - returns sum."""
    return a + b


def three_params(a, b, c):
    """Function with three parameters."""
    return a + b + c


def returns_none():
    """Function that returns None."""
    return None


def raises_error():
    """Function that raises an exception."""
    raise ValueError("This is a test error")


# Non-callable attributes for testing IsFunction
not_callable = "I am a string"
also_not_callable = 42
list_not_callable = [1, 2, 3]
