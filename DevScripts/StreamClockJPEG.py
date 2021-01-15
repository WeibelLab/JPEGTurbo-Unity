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


#
# Define TCPServer class we use for streaming
#

class JPEGStreamerClientHandler(socketserver.BaseRequestHandler):
  '''
  Handles a connection to our TCPServer.
  '''
  def __init__(self, request, client_address, server):
    self.logger = logging.getLogger('Client %s:%d'%(self.client_ip, self.client_port))
    self.client_ip = client_address[0]
    self.client_port = client_address[1]
    self.logger.info('Connected')
    self.startTime = time.time()
    socketserver.BaseRequestHandler.__init__(self, request, client_address, server)
    return
      
  def handle(self):
    '''Invoked when the client receives a message (does nothing)'''
    # Echo the back to the client 
    #data = self.request.recv(1024)
    #self.logger.debug('recv()->"%s"', data)
    #self.request.send(data)
    return
    
  def sendMessage(self, frame):
    '''sends the entire frame to the client. Returns the number of bytes sent (if less than len(frame) than client disconnected)'''
    
    # sends message length
    self.request.sendall(len(frame).to_bytes(4, "little"))
    
    # sends the entire message using send (instead of sendall) to avoid timeout issues
    totalsent = 0
    while totalsent < len(frame):
      sent = self.request.send(frame[totalsent:])
      if sent == 0:
          raise RuntimeError("socket connection broken after sending ")
      totalsent = totalsent + sent
    return len(frame)
    
  def finish(self):
    self.logger.info('Disconnected')
    return socketserver.BaseRequestHandler.finish(self)


class JPEGStreamerServer(socketserver.TCPServer):
  '''Streams JPEGs to all connected clients
     All JPEGs streamed will have the same resolution as backgroundImage
     
     @param backgroundImage numpy array with the image being encoded in the background (expecting BGR)
     @param fps frame rate
     @param preview whether or not we should call imshow to show what is being encoded
     
     Optional parameters:
     @param quality JPEG quality (defaults to 90)
     @param fontHeight how many pixels the font should take (defaults to 20)
     @param runMAXFPSTest if true, runs a loop of 100 frames to check for the maximum FPS we can create and encode JPEGs
  '''
  def __init__(self, server_address, backgroundImage, fps, preview, quality=90, fontHeight=20, runMAXFPSTest=True, handler_class=JPEGStreamerClientHandler):
    socketserver.TCPServer.__init__(self, server_address, handler_class)
    self.logger = logging.getLogger('JPEGSTreamerServer')
    self.logger.info('Listening at %s:%d' % (server_address[0], server_address[1]))
    
    # prepares to handle clients
    self._clients = set()
    self._backgroundImage = np.copy(backgroundImage)
    self._fontHeight = fontHeight
    self._imageWidth  = self._backgroundImage.shape[1]
    self._imageHeight = self._backgroundImage.shape[0]
    self._setupImageSettings()
    self._fps = fps
    self._jpegQuality = quality
    self._preview = preview
    
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
      
  def JPEGStreamingLoop(self):
    loopForever = True
    while loopForever:
      try:
        start = time.time()         # check how long it takes to encode and stream frame
        jpg = self.getEncodedJPEG() # creates JPEG
        
        for client in self._clients:
          client.sendMessage(jpg)
        
      except KeyboardInterrupt:
        self.logger.info("CTRL+C requested!")
        loopForever = False
      except:
        self.logger.error("Unhandled exception!")
        loopForever = False

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

  # listening loop
  t = threading.Thread(target=server.serve_forever)
  t.setDaemon(True) # don't hang on exit
  t.start()
  
  
  # streaming loop
  mainLogger.info("Starting streamer - press CTRL+C to stop at any time!")
  server.JPEGStreamingLoop()
  



