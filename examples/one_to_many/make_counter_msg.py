counter = 0

def step():
    global counter
    counter += 1
    print(f"Sending {counter} to pipeline_b and pipeline_c")
    return counter
