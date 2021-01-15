'''
 This python scripts creates a an image, writes the current time at its center,
 encodes it to a JPEG and streams it to any TCP clients connected to it. 
 TCP Clients first receive a 4 byte header (little endian) with the number of bytes
 of the JPEG image. Then, they receive those bytes.
 
 I developed this script to test my JPEGTurboUnityPlugin, but it can be easily adapted
 for other uses.
 
 Needs: OpenCV2 (cv2), numpy, simplejpeg
        (if you use conda, `conda install -c conda-forge opencv` AND `pip install simplejpeg`)
 
 
 author: Danilo Gasques (gasques@ucsd.edu)
'''

import socket
import argparse
import cv2
import numpy as np
import argparse
import sys
import logging
import time
import math
import simplejpeg
from datetime import datetime

# parse arguments
parser = argparse.ArgumentParser(description="TCPServer that streams a JPEG of current the current time")
parser.add_argument('width', nargs='?', type=int, help="image width", default=800)
parser.add_argument('height', nargs='?', type=int, help="image height", default=600)
parser.add_argument('fps', nargs='?', type=int, help="desired frame rate", default=30)
parser.add_argument('port', nargs='?', type=int, help="TCPServer's port (default=50000)", default=50000)
parser.add_argument('--background', nargs='?', type=str, help="desired background image", default="background.jpg")
parser.add_argument('--display', action='store_true', help="If set, shows the clock to the user (Warning: lowers max FPS)", default=False)
parser.add_argument('--quality', nargs='?', type=int, help="desired JPEG quality (default=90)", default=90)

args = parser.parse_args()

PORT = args.port
ADDR = "0.0.0.0"
WIDTH = args.width
if WIDTH < 0:
  WIDTH = 640

HEIGHT = args.height 
if HEIGHT < 0:
  HEIGHT = 480
  
QUALITY = args.quality

FPS = args.fps

print("Welcome to StreamclockJPEG!")
print("-"*20)
print("Preparing virtual clock...")

# helper function to write an entire chunck of data
def sendEntireMessage(sock, message):
    totalsent = 0
    while totalsent < len(message):
        sent = sock.send(message[totalsent:])
        if sent == 0:
            raise RuntimeError("socket connection broken")
        totalsent = totalsent + sent
    return len(message)

# Loads background image
print(" -> Loading background image %s" % args.background)
backgroundImage = cv2.imread(args.background, 1)

backgroundImageScaled = cv2.resize(backgroundImage, (WIDTH, HEIGHT), cv2.INTER_LANCZOS4)

# Finds a font size that makes the clock take only 20px at the center
timeNowStr = datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')
fontHeight = 20
fontSize = cv2.getFontScaleFromHeight(cv2.FONT_HERSHEY_SIMPLEX, fontHeight, 2)
textY = int(HEIGHT/2 - fontHeight/2)
textX = int(WIDTH/2 - cv2.getTextSize(timeNowStr, cv2.FONT_HERSHEY_SIMPLEX, fontSize,2)[0][0]/2)

# prints once for the sake of testing it and creating a window
image = cv2.putText(np.copy(backgroundImageScaled), timeNowStr, (textX, textY), cv2.FONT_HERSHEY_SIMPLEX,  
                   fontSize, (255, 255, 255), 2, cv2.LINE_AA) 
if args.display:                   
  cv2.imshow('time',image)
  cv2.waitKey(1)

# prints 100 times as fast as possible to figure out a cap for the frame rate ('cause OpenCV windows are not too fast)
# (includes copying background, writing text, and encoding to JPEG)
print(" -> Testing max FPS (display=%s)" % ("yes" if args.display else "no"))
startTime = time.time()
for i in range(100):
  timeNowStr = datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')
  image = cv2.putText(np.copy(backgroundImageScaled), timeNowStr, (textX, textY), cv2.FONT_HERSHEY_SIMPLEX,  
                   fontSize, (255, 255, 255), 2, cv2.LINE_AA) 
  #encimg = cv2.imencode('.jpg', image)                 
  encimg = simplejpeg.encode_jpeg(image, QUALITY, 'BGR')
  if args.display:                   
    cv2.imshow('time',image)
    cv2.waitKey(1)
    
if args.display:                   
  cv2.destroyAllWindows()
  
maxFPS = math.floor(1.0/((time.time() - startTime) / 100.0))
print(" -> able to create and encode %d frames per second with %dx%d (Quality=%d)" % (maxFPS, WIDTH,HEIGHT,QUALITY))



# opens a connection
#print("[%s] - Now, openning stream to %s:%d....\n\n" % (CAMERA,ADDR,PORT))
#s##tream = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
#3stream.connect((ADDR, PORT))
#print("[%s] - Successfully connected to stream on port %d" % (CAMERA, PORT))
# reading and processing loop (3 frames)
#frameCount = 3

#while frameCount > 0:
  # read header
#  width = int.from_bytes(receivedEntireMessage(stream, 4), "little")
#  height = int.from_bytes(receivedEntireMessage(stream, 4), "little")
#  colorLen = int.from_bytes(receivedEntireMessage(stream, 4), "little") # 0 if no color frame
#  depthLen = int.from_bytes(receivedEntireMessage(stream, 4), "little") # 0 if no depth frame
  
#  print("Got frame with (w=%d,h=%d,color=%d,depth=%d)" % (width, height, colorLen, depthLen))

  # read color if enabled
#  if (colorLen > 0):
#    colorData = receivedEntireMessage(stream, colorLen)

  # read depth if enabled
#  if (depthLen > 0):
#    depthData = receivedEntireMessage(stream, depthLen)

  # show color if enabled
#  if (colorLen > 0):
#    numpyarr = np.fromstring(colorData, np.uint8)
#    frame = cv2.imdecode(numpyarr, cv2.IMREAD_COLOR)
#    cv2.imwrite("%s-color-%d.jpeg" % (CAMERA,3-frameCount), frame)
#    cv2.imshow('Color', frame)
    

  # show depth if enabled
#  if (depthLen > 0):
#    deptharray = np.fromstring(depthData, np.uint16).reshape(height,width)
#    with open("%s-depth-%d.bin" % (CAMERA,3-frameCount), "wb") as f:
#      np.save(f, deptharray)
#    cv2.imshow('Depth', cv2.normalize(deptharray, dst=None, alpha=0, beta=65535, norm_type=cv2.NORM_MINMAX))

  # keeps going
#  frameCount -= 1

#  if (cv2.waitKey(1) & 0xFF == 27):
#    break

#stream.close()
