def step(input_data):
    doubled = input_data["pipeline_b"]
    squared = input_data["pipeline_c"]
    total = doubled + squared
    print(f"pipeline_d: Received doubled={doubled} and squared={squared}, sum={total}")
