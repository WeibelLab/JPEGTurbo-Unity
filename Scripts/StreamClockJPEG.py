import socket
import cv2
import numpy as np
import sys
import time
import json

PORT = 3614
ADDR = "localhost"
CPORT = 6606
CAMERA = "camera"

# do we have any parameters?
if len(sys.argv) != 4:
  print("Usage:\n\t %s port control_port camera_name" % sys.argv[0])
  exit(1)

# get port
try:
  PORT = int(sys.argv[1])
except:
  print("Invalid port %s" % sys.argv[1])
  exit(1)

try:
  CPORT = int(sys.argv[2])
except:
  print("Invalid control port %s" % sys.argv[2])
  exit(1)

# helper function to write an entire chunck of data
def sendEntireMessage(sock, message):
    totalsent = 0
    while totalsent < len(message):
        sent = sock.send(message[totalsent:])
        if sent == 0:
            raise RuntimeError("socket connection broken")
        totalsent = totalsent + sent
    return len(message)

# helper function to read an entire chunck of data
def receivedEntireMessage(sock, length):
  chunks = []
  bytes_recd = 0
  while bytes_recd < length:
      chunk = sock.recv(length - bytes_recd)
      if chunk == b'':
          raise RuntimeError("socket connection broken")
      chunks.append(chunk)
      bytes_recd = bytes_recd + len(chunk)
  return b''.join(chunks)


# get camera name
CAMERA = sys.argv[3]

print("[%s] - First, connecting to control port %s:%d....\n(Please, make sure control stream is running at the right port)\n\n" % (CAMERA,ADDR,CPORT))

# opens a control connection
CameraParameters = {}
controlStream = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
controlStream.connect((ADDR, CPORT))
cameraIsStreaming = False
while cameraIsStreaming != True:
    # send ping 
    sendEntireMessage(controlStream, b"\x0f\x00\x00\x00{\"type\":\"ping\"}")
    
    # read message
    msgLen = int.from_bytes(receivedEntireMessage(controlStream, 4), "little")
    msg = json.loads(receivedEntireMessage(controlStream, msgLen))
    cameraIsStreaming = msg["streaming"]
    if not cameraIsStreaming:
        print("[%s] - Camera is still not streaming... Waiting 5 seconds and trying again..." % CAMERA)
    else:
       # parse json
       CameraParameters = json.loads(msg["streamingCameraParameters"])
       CameraParameters["cameraType"] = msg["captureDeviceType"]
       CameraParameters["cameraSN"] = msg["captureDeviceSerial"]
       CameraParameters["filePrefix"] = CAMERA
       print("[%s] - Camera parameters: %s" % (CAMERA, CameraParameters))
       with open("%s-param.json"%CAMERA,"w") as f:
           f.write(json.dumps(CameraParameters, indent=2))
controlStream.close()

# opens a connection
print("[%s] - Now, openning stream to %s:%d....\n\n" % (CAMERA,ADDR,PORT))
stream = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
stream.connect((ADDR, PORT))
print("[%s] - Successfully connected to stream on port %d" % (CAMERA, PORT))
# reading and processing loop (3 frames)
frameCount = 3

while frameCount > 0:
  # read header
  width = int.from_bytes(receivedEntireMessage(stream, 4), "little")
  height = int.from_bytes(receivedEntireMessage(stream, 4), "little")
  colorLen = int.from_bytes(receivedEntireMessage(stream, 4), "little") # 0 if no color frame
  depthLen = int.from_bytes(receivedEntireMessage(stream, 4), "little") # 0 if no depth frame
  
  print("Got frame with (w=%d,h=%d,color=%d,depth=%d)" % (width, height, colorLen, depthLen))

  # read color if enabled
  if (colorLen > 0):
    colorData = receivedEntireMessage(stream, colorLen)

  # read depth if enabled
  if (depthLen > 0):
    depthData = receivedEntireMessage(stream, depthLen)

  # show color if enabled
  if (colorLen > 0):
    numpyarr = np.fromstring(colorData, np.uint8)
    frame = cv2.imdecode(numpyarr, cv2.IMREAD_COLOR)
    cv2.imwrite("%s-color-%d.jpeg" % (CAMERA,3-frameCount), frame)
    cv2.imshow('Color', frame)
    

  # show depth if enabled
  if (depthLen > 0):
    deptharray = np.fromstring(depthData, np.uint16).reshape(height,width)
    with open("%s-depth-%d.bin" % (CAMERA,3-frameCount), "wb") as f:
      np.save(f, deptharray)
    cv2.imshow('Depth', cv2.normalize(deptharray, dst=None, alpha=0, beta=65535, norm_type=cv2.NORM_MINMAX))

  # keeps going
  frameCount -= 1

  if (cv2.waitKey(1) & 0xFF == 27):
    break

stream.close()
