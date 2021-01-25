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
import socketserver
import threading
from datetime import datetime

#
# Creating basic logging mechanism
#
logging.basicConfig(level=logging.INFO,
                    format='[%(asctime)s] <%(name)s>: %(message)s',
                    )
mainLogger = logging.getLogger('Main')



class JPEGStreamerServer():
  '''Streams JPEGs to all connected clients
     All JPEGs streamed will have the same resolution as backgroundImage
     
     @param server_address tuple with ip:port (e.g., 0.0.0.0:5000)
     @param backgroundImage numpy array with the image being encoded in the background (expecting BGR)
     @param fps frame rate
     @param preview whether or not we should call imshow to show what is being encoded
     
     Optional parameters:
     @param quality JPEG quality (defaults to 90)
     @param fontHeight how many pixels the font should take (defaults to 20)
     @param runMAXFPSTest if true, runs a loop of 100 frames to check for the maximum FPS we can create and encode JPEGs
  '''
  def __init__(self, server_address, backgroundImage, fps, preview, quality=90, fontHeight=20, runMAXFPSTest=True):
    self.logger = logging.getLogger('JPEGSTreamerServer')
    self.logger.info('Listening at %s:%d' % (server_address[0], server_address[1]))
    
    # create a socket object
    self.serverSocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    self.serverSocket.bind((server_address[0], server_address[1]))
    self.serverSocket.listen(5)
    
    # prepares to handle clients
    self._clients = set()
    
    # prepares backgroud
    self._backgroundImage = np.copy(backgroundImage)
    self._fontHeight = fontHeight
    self._imageWidth  = self._backgroundImage.shape[1]
    self._imageHeight = self._backgroundImage.shape[0]
    self._setupImageSettings()
    self._fps = fps
    self._jpegQuality = quality
    self._preview = preview
    
    # makes sure that clients won't get disconnected if they don't send anything
    # (see https://docs.python.org/3/library/socketserver.html#socketserver.BaseServer.timeout)
    self.timeout = None
    
    # finds the max FPS for the server
    if runMAXFPSTest:
      self.FindMaxFrameRate()
    return
    
  def _setupImageSettings(self):
    timeNowStr = datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')
    self._fontSize = cv2.getFontScaleFromHeight(cv2.FONT_HERSHEY_SIMPLEX, self._fontHeight , 2)
    self._textY = int(self._imageHeight/2 - self._fontHeight /2)
    self._textX = int(self._imageWidth/2 - cv2.getTextSize(timeNowStr, cv2.FONT_HERSHEY_SIMPLEX, self._fontSize,2)[0][0]/2)
  
  def getEncodedJPEG(self):
    '''returns a buffef with an encoded JPEG'''
    timeNowStr = datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')
    image = cv2.putText(np.copy(self._backgroundImage), timeNowStr, (self._textX, self._textY), cv2.FONT_HERSHEY_SIMPLEX,  
                     self._fontSize, (255, 255, 255), 2, cv2.LINE_AA)   
    encimg = simplejpeg.encode_jpeg(image, self._jpegQuality, 'BGR') # faster alternative to OPENCV: result, encimg = cv2.imencode('.jpg', image)             
    if self._preview:                   
      cv2.imshow('time',image)
      cv2.waitKey(1)
    return encimg
  
  def FindMaxFrameRate(self):
    self.logger.info(" -> Testing max FPS (preview=%s)" % ("yes" if self._preview else "no"))
    startTime = time.time()
    for i in range(100):
      self.getEncodedJPEG()

    maxFPS = math.floor(1.0/((time.time() - startTime) / 100.0))
    self.logger.info(" -> able to create and encode %d frames per second with %dx%d (Quality=%d)" % (maxFPS, self._imageWidth,self._imageHeight,self._jpegQuality))

    if self._fps > maxFPS:
      self.logger.info("Setting FPS to %d (%d is greater than maximum we can achieve)"%(maxFPS,self._fps))
      self._fps = maxFPS
      
  def WaitForClients(self):
    # listening loop
    self.clientThread = threading.Thread(target=self.serve_forever)
    self.clientThread.setDaemon(True)
    self.clientThread.start()
    
  def serve_forever(self):
    while True:
      # accept any new connection
      sockt, addr = self.serverSocket.accept()
      self.logger.info("Client connected %s:%d" % addr)
      self._clients.add((sockt, addr))
      
  def sendMessageToClient(self, socket, message):
    '''sends the entire frame to the client. Returns the number of bytes sent (if less than len(frame) than client disconnected)'''
    
    try:
      # sends message length
      socket.sendall(len(message).to_bytes(4, "little"))
      
      # sends the entire message using send (instead of sendall) to avoid timeout issues
      totalsent = 0
      while totalsent < len(message):
        sent = socket.send(message[totalsent:])
        if sent == 0:
            return totalsent
        totalsent = totalsent + sent
      return len(message)
    
    except ConnectionAbortedError as e:
      return 0
    except:
      return 0
    
     
  def JPEGStreamingLoop(self):
    loopForever = True
    maxTimePerFrame = (1.0 / self._fps) - 0.005
    while loopForever:
      try:
        startTime = time.time()         # check how long it takes to encode and stream frame
        jpg = self.getEncodedJPEG() # creates JPEG
        
        removalSet = set()
        
        for client in self._clients:
          if self.sendMessageToClient(client[0],jpg) < len(jpg):
            removalSet.add(client)
            
        for client in removalSet:
          self._clients.remove(client)
          self.logger.info("Client disconnected %s:%d" % client[1])
          
        remaingSleepTime = maxTimePerFrame - (time.time() - startTime)
        if remaingSleepTime > 0:
          time.sleep(remaingSleepTime)
        else:
          self.logger.info("We took too long (%f sec instead of %f sec)" % (maxTimePerFrame-remaingSleepTime,maxTimePerFrame))
        
      except KeyboardInterrupt:
        self.logger.info("CTRL+C requested!")
        loopForever = False
      #except:
      #  self.logger.error("Unhandled exception!")
      #  loopForever = False
      

#
# ------------------------------------------------------------------------------------------------------------------------------------------------------
#



if __name__ == '__main__':
  #
  # Parsing script arguments
  #

  parser = argparse.ArgumentParser(description="TCPServer that streams a JPEG of current the current time")
  parser.add_argument('width', nargs='?', type=int, help="image width", default=800)
  parser.add_argument('height', nargs='?', type=int, help="image height", default=600)
  parser.add_argument('fps', nargs='?', type=int, help="desired frame rate", default=30)
  parser.add_argument('port', nargs='?', type=int, help="TCPServer's port (default=50000)", default=50000)
  parser.add_argument('--background', nargs='?', type=str, help="desired background image", default="background.jpg")
  parser.add_argument('--display', action='store_true', help="If set, shows the clock to the user (Warning: lowers max FPS)", default=False)
  parser.add_argument('--quality', nargs='?', type=int, help="desired JPEG quality (default=90)", default=90)

  args = parser.parse_args()

  #
  # Setting up variables that we use in the rest of the script
  #

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

  mainLogger.info("Welcome to StreamclockJPEG!")
  mainLogger.info("-"*20)

  # Loads background image
  mainLogger.info(" Loading background image %s" % args.background)
  backgroundImage = cv2.imread(args.background, 1)
  backgroundImageScaled = cv2.resize(backgroundImage, (WIDTH, HEIGHT), cv2.INTER_LANCZOS4)
  
  #
  # Starting server
  #
  
  address = (ADDR, PORT)
  server = JPEGStreamerServer(address, backgroundImageScaled, FPS, args.display, QUALITY)

  # listening loop (waits for new connections)  - runs in a separate thread
  server.WaitForClients()
  
  # streaming loop
  mainLogger.info("Starting streamer - press CTRL+C to stop at any time!")
  server.JPEGStreamingLoop()
  



