counter = 0

def step():
    global counter
    counter += 1
    print(f"pipeline_a: Sending number {counter}")
    return counter
