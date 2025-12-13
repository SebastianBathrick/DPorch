delta_time = 0.0
elapsed_time = 0.0
sec_passed = 0

def step():
    global delta_time, elapsed_time, sec_passed
    elapsed_time += delta_time
    
    if (elapsed_time >= 1.0):
        sec_passed += 1
        elapsed_time = 0
        print(f"{sec_passed} second(s) have passed")