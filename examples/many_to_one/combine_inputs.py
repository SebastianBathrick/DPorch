def step(input_data):
    random_num = input_data["pipeline_x"]
    timestamp = input_data["pipeline_y"]
    print(f"Received random number {random_num} and timestamp {timestamp}")
