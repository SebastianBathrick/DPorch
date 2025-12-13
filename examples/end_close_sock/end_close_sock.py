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