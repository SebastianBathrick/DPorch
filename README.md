# DPorch
DPorch is a distributed pipeline framework written in C# for building configurable, TCP-connected data-processing pipelines.

* **Pipeline Execution** - Each pipeline is a network node that independently runs user-generated Python code in its own continuous iteration loop, processing data as it arrives and immediately beginning the next iteration after sending results.
* **Script Chaining** - Each pipeline is defined by a JSON config containing an ordered list of Python scripts that execute sequentially in each iteration. Each script defines a step() function that receives the previous script’s output as input, with the final script’s return value sent to connected target pipelines.
* **Pipeline Communication** - Pipelines discover each other on the local network using names defined in their configs and establish TCP connections. Data is automatically serialized, transmitted, and deserialized, then provided to the target pipelines' first scripts as input. When a pipeline receives data from source(s), values are provided as a dictionary with keys matching source pipeline name(s) (e.g., input["rand_num"], input["adder"]).

### Technologies
- **C# 14**
- **.NET 9.0**
- **Python 3.7+** (via Python.Net)
- **NetMQ** (ZeroMQ), TCP/UDP (System.Net.Sockets)
- **JSON** configuration (System.Text.Json)
- **pickle** serialization

## Table of Contents
- [Prerequisites](#prerequisites)
- [Quick Start Guide](#quick-start-guide)
- [Command Line Interface](#command-line-interface)
- [Pipeline Configurations](#pipeline-configurations)
- [Python Scripting](#python-scripting)
  - [Step Function](#step-function)
  - [End Function](#end-function)
  - [Delta Time](#delta-time)
- [Pipeline Communication](#pipeline-communication)
  - [One Source to Multiple Targets](#one-source-to-multiple-targets)
  - [Multiple Sources to One Target](#multiple-sources-to-one-target)
  - [Diamond Pipeline Topology Example](#diamond-pipeline-topology-example)

# Prerequisites

- **.NET 9.0 SDK or later** - [Download here](https://dotnet.microsoft.com/download)
  - Verify installation: `dotnet --version`
  
- **Python 3.7 or later** - [Download here](https://www.python.org/downloads/)
  - Verify installation: `python --version`
  - **Important**: Note the location of your Python DLL file (e.g., `python311.dll`) - you'll need this during setup
# Quick Start Guide

## 1. Clone and Build
Clone the repository and navigate to its root directory:
```powershell
git clone https://github.com/SebastianBathrick/DPorch
cd DPorch
```

Restore dependencies and build the solution:
```powershell
dotnet restore
dotnet build
```

## 2. Run First-Time Setup
Navigate to the CLI directory:
```powershell
cd .\src\DPorch.CLI\
```

Start DPorch to begin the setup wizard:
```powershell
dotnet run
```

You'll see:
```
Initialized: Created new preferences file at C:\Users\Sebastian\AppData\Roaming\DPorch\settings.json

Required preferences have not been assigned values
You can assign them now using prompts or later using the pref command with the appropriate options

Would you like to set required preferences now? [y/n] (y):
```

Press `y` and `ENTER` to continue.

## 3. Configure Settings

**Python DLL Path**  
Enter the path to your Python 3.7+ DLL:
```
Please enter Python v3.7+ DLL path: C:\Users\Name\AppData\Local\Programs\Python\Python311\python311.dll
```

**Input Network Interface**  
Select the interface where pipelines will listen for incoming TCP connections. Use arrow keys to navigate, then press `ENTER`:
```
Select input network interface:
> Wi-Fi
  Loopback Pseudo-Interface 1
  Local Area Connection* 9
  ...
```

**Output Network Interfaces**  
Select one or more interfaces that pipelines will scan to discover other pipelines. Use arrow keys to navigate and `SPACEBAR` to select. Press `ENTER` when done:
```
Select output network interfaces:
> [X] Wi-Fi
  [ ] Loopback Pseudo-Interface 1
  [ ] Local Area Connection* 9
  ...
```

**Service Discovery Port**  
Enter the UDP port for pipeline discovery (default: 5557):
```
Please enter service discovery port (1-65535) (5557): 5557
Saved: Discovery port: 5557
Preferences file setup complete!
```
## 4. Create Your First Pipelines

This section shows you how to create two pipelines that communicate across machines.

### Machine 1: Create Pipeline A

Create a working directory:
```powershell
mkdir tutorial
cd tutorial
```

Create Pipeline A that will send data:
```powershell
dporch init -n pipeline_a -i 0 -o pipeline_b -s generate_number.py
```

The flags specify:
- `-n pipeline_a` - Names the pipeline
- `-i 0` - No input sources (it generates data)
- `-o pipeline_b` - Sends output to pipeline_b
- `-s generate_number.py` - Uses this Python script

Create and edit `generate_number.py`:
```python
import time
counter = 0

def step():
    global counter
    counter += 1
    time.sleep(1)
    print(f"Sending: {counter}")
    return counter
```

### Machine 2: Create Pipeline B

On the second machine, create a directory:
```powershell
mkdir tutorial
cd tutorial
```

Create Pipeline B that will receive data:
```powershell
dporch init -n pipeline_b -i 1 -s print_number.py
```

The flags specify:
- `-n pipeline_b` - Names the pipeline
- `-i 1` - Expects data from 1 source pipeline
- `-s print_number.py` - Uses this Python script

Create and edit `print_number.py`:
```python
def step(input_data):
    number = input_data["pipeline_a"]
    print(f"Received: {number}")
```

### Run the Pipelines

On **Machine 1**, run Pipeline A:
```powershell
dporch run pipeline_a.json
```

On **Machine 2**, run Pipeline B:
```powershell
dporch run pipeline_b.json
```

You should see Pipeline A sending numbers and Pipeline B receiving them. Press `CTRL+C` to stop either pipeline.

For more complex pipeline topologies and advanced features, continue reading the sections below.

# Command Line Interface
DPorch is controlled using an option-based CLI. To run a command, you use DPorch’s executable path and follow it with options. To see a list of available commands, type into the terminal:
```powershell
PS C:\ dporch --help
```
The following will be displayed:
```powershell
USAGE:
    dporch [OPTIONS] <COMMAND>

EXAMPLES:
    dporch init
    dporch init --name my_pipeline
    dporch init -n my_pipeline -s script.py -i 2 -o target1
    dporch run pipeline.json
    dporch run pipeline_1.json pipeline_2.json

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    init     Create a new pipeline configuration file
    run      Execute a pipeline configuration
    prefs    Manage user preferences. Omit options to view all preferences
```
To see more information about a specific command, type into the terminal:
```powershell
dporch <COMMAND --help
```

# Pipeline Configurations

To create a new pipeline configuration, use the `init` command.
```
PS C:\ dporch init
Created: C:\Computer\config.json
```

The command creates a `.json` file in the current working directory that contains the properties necessary to define a pipeline. It will look like this:
```json
{
  "name": "",
  "scripts": [],
  "source_pipeline_count": 0,
  "target_pipeline_names": []
}
```

## Pipeline Properties

* **name** (string) - The name of the pipeline. This name is used by other pipelines to reference this pipeline in their `target_pipeline_names` list and appears as a dictionary key when this pipeline sends data to others. This name must be at least three characters long.
* **scripts** - Python script file paths relative to the configuration file directory to execute sequentially in each iteration. Each script must define a `step()` function. There must be at least one script file path.
* **source_pipeline_count** - The number of source pipelines this pipeline expects to receive data from. The pipeline will wait for data from all sources before beginning each iteration. This number can be zero or greater.
* **target_pipeline_names** - Pipeline names to send this pipeline's output data to. The final script's return value will be sent to all targets listed here. There can be zero or more target pipelines.

# Python Scripting
Python scripts define pipeline behavior at every phase of its lifecycle: when it starts up, during each iteration, and when it shuts down. Each script has lifecycle hooks similar to React components or Unity MonoBehaviours—code that runs once on startup, code that runs repeatedly, and cleanup code.
> [!NOTE]
> [/examples](/examples) contains files for each example shown below.


## Step Function
Each Python script requires a top-level function named `step` with an optional parameter. The following is a bare-minimum but valid script:
```python
def step():
    pass
```

Each script's top-level statements run once when the pipeline starts (after receiving handshakes from its source pipelines), and scripts execute in the order specified in the JSON configuration. After sending handshakes to its target pipelines, the pipeline begins its iteration loop, executing each script's `step()` function once per iteration in the specified order. Every script has its own isolated scope, and top-level global variables maintain state between iterations.

Let's pretend the script below is the only script in a given pipeline:
```python
counter = 0
print(f"Initial counter value {counter}")

def step():
    global counter
    counter += 1
    print(f"Counter value this iteration: {counter}")
```

If it were allowed to run three iterations, the output would look like the following. Note how the top-level `print` statement executes only once, while the `step()` function's `print` statement executes on each iteration.
```
Initial counter value 0
Counter value this iteration: 1
Counter value this iteration: 2
Counter value this iteration: 3
```

Then, let's say we added another script to the end of the pipeline with the following code:
```python
def step(input_data):
    if input_data % 2 == 0:
        print(f"{input_data} is an even number")
    else:
        print(f"{input_data} is an odd number")
```

To work with this second script, we'll modify the first one slightly by having the `step()` function return the value of `counter`.
```python
counter = 0
print(f"Initial counter value {counter}")

def step():
    global counter
    counter += 1
    print(f"Counter value this iteration: {counter}")
    return counter
```

Now, with the pipeline having both scripts, if we execute it, then it would output the following. Notice how the `counter` value from the first script is passed as `input_data` to the second script, which then checks whether it's even or odd.
```
Initial counter value 0
Counter value this iteration: 1
1 is an odd number
Counter value this iteration: 2
2 is an even number
Counter value this iteration: 3
3 is an odd number
```

## End Function

Each script can optionally define an `end` function that is called when the pipeline shuts down, such as when the user sends a keyboard interrupt (`CTRL+C`) in the terminal. The `end()` function takes no parameters and is useful for cleanup operations like releasing resources or closing connections. Like `step()` functions, `end()` functions execute sequentially in the order scripts are defined in the configuration.

The following is a valid script with an `end()` function:
```python
def step():
    print("Running iteration")

def end():
    print("Cleaning up resources")
```

When a pipeline with this script receives a shutdown signal (`CTRL+C`), it will call the `end()` function before terminating. If a script does not define an `end()` function, DPorch will skip it and move to the next script.

Here's a practical example that demonstrates resource management using the `end()` function. This script creates a TCP socket connection, uses it during iterations, and properly closes it during cleanup:

**end_close_sock.json**
```json
{
  "name": "end_close_sock",
  "scripts": [
    "..\\iteration_rate_limiter.py",
    "end_close_sock.py"
  ],
  "source_pipeline_count": 0,
  "target_pipeline_names": []
}
```

**end_close_sock.py**
```python
import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(("tcpbin.com", 4242))  # Public echo server

def step():
    sock.send(b"Hello from DPorch!\n")
    response = sock.recv(1024)
    print(f"Received: {response.decode('utf-8')}")
    return response

def end():
    sock.close()
    print("Socket closed")
```

The script creates a socket connection at the top level, which executes once when the pipeline starts. Each iteration sends a message and receives a response using the `step()` function. When the pipeline shuts down (via `CTRL+C`), the `end()` function is called automatically, ensuring the socket is properly closed before the program terminates.

## Delta Time

DPorch provides a special managed variable called `delta_time` that automatically tracks the elapsed time (in seconds) since the previous `step()` function call for that script. To use it, declare a top-level variable named `delta_time` in your script. DPorch will detect this variable and automatically update it before each `step()` function call with the time elapsed since that script's previous `step()` execution.

The following script demonstrates basic usage of `delta_time`:
```python
delta_time = 0.0

def step():
    print(f"Time since last step() call: {delta_time} seconds")
```

On the first `step()` call, `delta_time` will be `0.0` because the script hasn't been executed yet. On subsequent calls, it will contain the actual elapsed time since the previous `step()` execution for that script.

Here's a practical example that uses `delta_time` to count seconds. The script accumulates elapsed time and prints a message each time a full second passes:

**delta_time_sec.json**
```json
{
  "name": "delta_time_sec",
  "scripts": [
    "delta_time_sec.py"
  ],
  "source_pipeline_count": 0,
  "target_pipeline_names": []
}
```

**delta_time_sec.py**
```python
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
```

The script maintains an `elapsed_time` accumulator that adds the `delta_time` from each iteration. When the accumulated time reaches or exceeds one second, it increments the `sec_passed` counter, resets the accumulator, and prints the total seconds elapsed. DPorch automatically updates `delta_time` before each iteration, so the script doesn't need to manually track timing.

# Pipeline Communication

A DPorch pipeline can send output data to zero or more target pipelines and receive input data from zero or more source pipelines, enabling flexible network topologies.

## One Source to Multiple Targets

In this example, `pipeline_a` sends its output to both `pipeline_b` and `pipeline_c`.

<img src="/.github/images/one-to-many-ex.svg" width=480 alt="Diagram showing one node labeled pipeline_a with one arrow pointing at pipeline_b and another pointing at pipeline_c">

**pipeline_a.json**
```json
{
  "name": "pipeline_a",
  "scripts": ["make_counter_msg.py"],
  "source_pipeline_count": 0,
  "target_pipeline_names": [
     "pipeline_b",
     "pipeline_c"
   ]
}
```

The configuration for `pipeline_a` specifies two target pipelines in its `target_pipeline_names` array. This means whatever value the final script returns will be sent to both `pipeline_b` and `pipeline_c`. Since `source_pipeline_count` is `0`, this pipeline doesn't wait for any incoming data before starting its iterations.

**make_counter_msg.py**
```python
counter = 0

def step():
    global counter
    counter += 1
    print(f"Sending {counter} to pipeline_b and pipeline_c")
    return counter
```

The script maintains a `counter` variable that increments with each iteration. The `step()` function returns the current counter value, which DPorch automatically sends to all pipelines listed in `target_pipeline_names`.

**pipeline_b.json**
```json
{
  "name": "pipeline_b",
  "scripts": ["print_counter_msg.py"],
  "source_pipeline_count": 1,
  "target_pipeline_names": []
}
```

**pipeline_c.json**
```json
{
  "name": "pipeline_c",
  "scripts": ["print_counter_msg.py"],
  "source_pipeline_count": 1,
  "target_pipeline_names": []
}
```

Both `pipeline_b` and `pipeline_c` have similar configurations. Each has `source_pipeline_count` set to `1`, meaning they wait to receive data from one source pipeline before beginning each iteration. They have no target pipelines, so they don't send data to anyone else. Both pipelines use the same script file, `print_counter_msg.py`.

**print_counter_msg.py**
```python
def step(input_data):
    msg = input_data["pipeline_a"]
    print(f"I got a message from pipeline_a: {msg}")
```

When a pipeline receives data from source pipelines, DPorch bundles the data as a dictionary where the keys are the source pipeline names and the values are the data the keyed pipeline sent. Here, both `pipeline_b` and `pipeline_c` access the incoming counter value using `input_data["pipeline_a"]` because `pipeline_a` is the name of the source.

When these three pipelines run together, they produce the following output:

**pipeline_a output**
```
Sending 1 to pipeline_b and pipeline_c
Sending 2 to pipeline_b and pipeline_c
Sending 3 to pipeline_b and pipeline_c
(continues...)
```

**pipeline_b output**
```
I got a message from pipeline_a: 1
I got a message from pipeline_a: 2
I got a message from pipeline_a: 3
(continues...)
```

**pipeline_c output**
```
I got a message from pipeline_a: 1
I got a message from pipeline_a: 2
I got a message from pipeline_a: 3
(continues...)
```

Each time `pipeline_a` completes an iteration, it sends its return value to both target pipelines simultaneously. Both `pipeline_b` and `pipeline_c` receive the same data and process it independently in their own iteration loops.

## Multiple Sources to One Target

In this example, both `pipeline_x` and `pipeline_y` send their outputs to `pipeline_z`.

<img src="/.github/images/many-to-one-ex.svg" width=480 alt="Diagram showing a node labeled pipeline_x and another labeled pipeline_y both pointing at a third node labeled pipeline_z">

**pipeline_x.json**
```json
{
  "name": "pipeline_x",
  "scripts": ["generate_random.py"],
  "source_pipeline_count": 0,
  "target_pipeline_names": ["pipeline_z"]
}
```

**generate_random.py**
```python
import random

def step():
    num = random.randint(1, 100)
    print(f"pipeline_x generated: {num}")
    return num
```

**pipeline_y.json**
```json
{
  "name": "pipeline_y",
  "scripts": ["generate_timestamp.py"],
  "source_pipeline_count": 0,
  "target_pipeline_names": ["pipeline_z"]
}
```

**generate_timestamp.py**
```python
import time

def step():
    timestamp = int(time.time())
    print(f"pipeline_y generated timestamp: {timestamp}")
    return timestamp
```

**pipeline_z.json**
```json
{
  "name": "pipeline_z",
  "scripts": ["combine_inputs.py"],
  "source_pipeline_count": 2,
  "target_pipeline_names": []
}
```

**combine_inputs.py**
```python
def step(input_data):
    random_num = input_data["pipeline_x"]
    timestamp = input_data["pipeline_y"]
    print(f"Received random number {random_num} and timestamp {timestamp}")
```

The key difference here is that `pipeline_z` has `source_pipeline_count` set to `2`, which means it waits to receive data from both `pipeline_x` and `pipeline_y` before beginning each iteration. The incoming data is accessed using the source pipeline names as dictionary keys: `input_data["pipeline_x"]` and `input_data["pipeline_y"]`.

When these three pipelines run together, they produce the following output:

**pipeline_x output**
```
pipeline_x generated: 42
pipeline_x generated: 87
pipeline_x generated: 15
(continues...)
```

**pipeline_y output**
```
pipeline_y generated timestamp: 1702393845
pipeline_y generated timestamp: 1702393846
pipeline_y generated timestamp: 1702393847
(continues...)
```

**pipeline_z output**
```
Received random number 42 and timestamp 1702393845
Received random number 87 and timestamp 1702393846
Received random number 15 and timestamp 1702393847
(continues...)
```

Notice how `pipeline_z` only processes data after receiving input from both sources. This synchronization is automatic—DPorch waits until all expected source pipelines have transmitted their data before starting the iteration.

## Diamond Pipeline Topology Example

In this example, `pipeline_a` generates numbers and sends them to both `pipeline_b` and `pipeline_c`. Each of these pipelines performs a different mathematical operation, then both send their results to `pipeline_d`, which combines them.

<img src="/.github/images/diamond-ex.svg" width=480 alt="Diagram showing pipeline_a pointing to both pipeline_b and pipeline_c, which both point to pipeline_d">

### pipeline_a - Number Generator

**pipeline_a.json**
```json
{
  "name": "pipeline_a",
  "scripts": ["generate_number.py"],
  "source_pipeline_count": 0,
  "target_pipeline_names": [
     "pipeline_b",
     "pipeline_c"
   ]
}
```

**generate_number.py**
```python
counter = 0

def step():
    global counter
    counter += 1
    print(f"pipeline_a: Sending number {counter}")
    return counter
```

`pipeline_a` generates incrementing numbers and sends each one to both `pipeline_b` and `pipeline_c`.

### pipeline_b - Doubler

**pipeline_b.json**
```json
{
  "name": "pipeline_b",
  "scripts": ["double_number.py"],
  "source_pipeline_count": 1,
  "target_pipeline_names": ["pipeline_d"]
}
```

**double_number.py**
```python
def step(input_data):
    num = input_data["pipeline_a"]
    doubled = num * 2
    print(f"pipeline_b: Doubled {num} to {doubled}")
    return doubled
```

`pipeline_b` receives numbers from `pipeline_a`, doubles them, and sends the result to `pipeline_d`.

### pipeline_c - Squarer

**pipeline_c.json**
```json
{
  "name": "pipeline_c",
  "scripts": ["square_number.py"],
  "source_pipeline_count": 1,
  "target_pipeline_names": ["pipeline_d"]
}
```

**square_number.py**
```python
def step(input_data):
    num = input_data["pipeline_a"]
    squared = num ** 2
    print(f"pipeline_c: Squared {num} to {squared}")
    return squared
```

`pipeline_c` receives numbers from `pipeline_a`, squares them, and sends the result to `pipeline_d`.

### pipeline_d - Combiner

**pipeline_d.json**
```json
{
  "name": "pipeline_d",
  "scripts": ["combine_results.py"],
  "source_pipeline_count": 2,
  "target_pipeline_names": []
}
```

**combine_results.py**
```python
def step(input_data):
    doubled = input_data["pipeline_b"]
    squared = input_data["pipeline_c"]
    total = doubled + squared
    print(f"pipeline_d: Received doubled={doubled} and squared={squared}, sum={total}")
```

`pipeline_d` waits for data from both `pipeline_b` and `pipeline_c`, then combines the results by adding them together.

### Output

When these four pipelines run together, they produce the following output:

**pipeline_a output**
```
pipeline_a: Sending number 1
pipeline_a: Sending number 2
pipeline_a: Sending number 3
pipeline_a: Sending number 4
(continues...)
```

**pipeline_b output**
```
pipeline_b: Doubled 1 to 2
pipeline_b: Doubled 2 to 4
pipeline_b: Doubled 3 to 6
pipeline_b: Doubled 4 to 8
(continues...)
```

**pipeline_c output**
```
pipeline_c: Squared 1 to 1
pipeline_c: Squared 2 to 4
pipeline_c: Squared 3 to 9
pipeline_c: Squared 4 to 16
(continues...)
```

**pipeline_d output**
```
pipeline_d: Received doubled=2 and squared=1, sum=3
pipeline_d: Received doubled=4 and squared=4, sum=8
pipeline_d: Received doubled=6 and squared=9, sum=15
pipeline_d: Received doubled=8 and squared=16, sum=24
(continues...)
```

Notice how `pipeline_d` only processes data after receiving input from both `pipeline_b` and `pipeline_c`. For example, when the original number is 3:
- `pipeline_b` doubles it to 6
- `pipeline_c` squares it to 9
- `pipeline_d` receives both and adds them: 6 + 9 = 15

# Additional Documentation

For detailed technical documentation, including threading model, design patterns, and implementation details, see [ARCHITECTURE.md](/docs/ARCHITECTURE.md).

# Contributing

Contributions are welcome! Please feel free to submit issues or pull requests.

# License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for more details.
