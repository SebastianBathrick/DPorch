def step(input_data):
    num = input_data["pipeline_a"]
    squared = num ** 2
    print(f"pipeline_c: Squared {num} to {squared}")
    return squared
