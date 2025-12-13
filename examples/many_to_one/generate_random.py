import random

def step():
    num = random.randint(1, 100)
    print(f"pipeline_x generated: {num}")
    return num
