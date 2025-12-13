import time
did_show_msg = False

def step(input_data):
    global did_show_msg
    
    if not did_show_msg:
        print("\033[33mNote:\033[0m Pipeline(s) have been configured with \033[36m\"examples\\iteration_rate_limiter.py\"\033[0m to limit the iteration rate.")
        print("To remove this limiter, locate pipeline configuration(s) containing the rate limiter in the \033[1mscripts\033[0m property and remove its script path.")
        did_show_msg = True
    time.sleep(1)
    return input_data