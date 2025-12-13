import time

def step():
    timestamp = int(time.time())
    print(f"pipeline_y generated timestamp: {timestamp}")
    return timestamp
