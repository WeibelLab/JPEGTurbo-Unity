'''
 The JPEGStreamerTCPServer class belongs to the Weibel Lab's JPEGTurboUnityPlugin
 
 JPEGStreamerTCPServer, used as a module, allows one to set up a server that streams an array of frames (in a loop).
 The implementation here is slightly different from the implementation used in StreamClockJPEG.py
 
 JPEGStreamerTCPServer has three modes:
   -- user triggered stream
   -- fixed FPS stream


 Under the hood:
 
   JPEGStreamerTCPServer creates encodes images to JPEG using libjpegturbo (simplejped) and streams them
 to any TCP clients connected to it. 

   This implementation is slightly different from the implementation used  
   I developed this script to test my JPEGTurboUnityPlugin, but it can be easily adapted
 for other uses.
 
 Requirements: OpenCV2 (cv2), numpy, simplejpeg
        (if you use conda, `conda install -c conda-forge opencv` AND `pip install simplejpeg`)
        
 Notice: OpenCV2 is only imported when used as script but not as a module
 
 
 author: Danilo Gasques (gasques@ucsd.edu)
'''

import socket
import numpy as np
import argparse
import sys
import logging
import time
import math
import simplejpeg
import socketserver
import threading


#
# Creating basic logging mechanism
#
logging.basicConfig(level=logging.INFO,
                    format='[%(asctime)s] <%(name)s>: %(message)s',
                    )
mainLogger = logging.getLogger('JPEGStreamer')



class JPEGStreamerServer():
  '''Streams JPEGs to all connected clients
     All JPEGs streamed will have the same resolution as backgroundImage
     
     @param server_address tuple with ip:port (e.g., 0.0.0.0:5000)
     @param frameArray numpy array with array of images that will be sent to the user (images should be in BGR)
     
     if @param frameArray is set to None, JPEGStreamerServer can still serve frames by having the user call the method
       `encodeAndSendJPEGToAllClients` or `sendJPEGToAllClients`
       
     if @param frameArray is an numpy array of frames
        then calling `sendNextFrame` or `streamForever` will use frames from the array.
        
        if @param preencodeFrames is set to true, then all frames will be pre-encoded as JPEGs and stored in memory
        
        @param colorspace (defaults to BGR - OpenCV friendly) defines the colorspace used by the images in the array
         Available options `RGB’, ‘BGR’, ‘RGBX’, ‘BGRX’, ‘XBGR’, ‘XRGB’, ‘GRAY’, ‘RGBA’, ‘BGRA’, ‘ABGR’, ‘ARGB’, ‘CMYK’
     
     Optional parameters:
     @param fps frame rate when using RunStreamingLoop (defaults to 30)
     @param quality JPEG quality (defaults to 90)
     @param preencodeFrames if true (default), preencodes all frames and store them in memory
     @param maxClients (defaults to 5) limits the number of clients connected
  '''
  def __init__(self, server_address, frameArray, fps=30, quality=90, preencodeFrames=True, maxClients=5, colorspace='BGR'):
    self.logger = logging.getLogger('JPEGSTreamerServer')
    self.logger.info('Configured to listen on %s:%d' % (server_address[0], server_address[1]))
    
    # create a socket object
    self.serverSocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    self.serverSocket.bind((server_address[0], server_address[1]))
    self.serverSocket.listen(maxClients)
    
    # prepares to handle clients
    self._clients = set()
    
    # prepares content for streaming
    self._fps = fps
    self._jpegQuality = quality
    self._preEncoded = preencodeFrames
    self._frames = frameArray
    self._frameColorspace = colorspace
    self._encodedFrames = None
    self._currentFrame = 0
    self._frameCount = 0

    # infinite loop related stuff
    self.loopForever = False
        
    # did the user provide any frames?
    if (self._frames != None):
      self._frameCount = len(self._frames)
      self.sendNextFrame = self._sendNextFrameRaw
      
      # encode frames only once
      if (self._preEncoded == True): 
        self.logger.info('Pre-encoding %d frames...' % self._frameCount)
        self._encodedFrames = []
        for frame in self._frames:
          self._encodedFrames.append(self.encodeJPEG(frame))
        self.logger.info('Pre-encoded %d frames in %f seconds' % len(self._frames))
        self.sendNextFrame = self._sendNextFrameEncoded # makes sure that calling sendNextFrame uses the pre-encoded list
        self._frameCount = len(self._encodedFrames)
        self._frames = None # we should not keep a reference to the raw frames
        
    # makes sure that clients won't get disconnected if they don't send anything
    # (see https://docs.python.org/3/library/socketserver.html#socketserver.BaseServer.timeout)
    self.timeout = None
    
    # finds the max FPS for the server
    if runMAXFPSTest:
      self.FindMaxFrameRate()
    return
   
  
  #
  # JPEG encoding
  #
   
  def encodeJPEG(self, image):
    '''Returns a buffer with an encoded JPEG'''
    encimg = simplejpeg.encode_jpeg(image, self._jpegQuality, self._frameColorspace) # faster alternative to OPENCV: result, encimg = cv2.imencode('.jpg', image)
    return encimg
  
  #
  # Methods to manage client connection
  #
  
  def ListenForClients(self):
    # starts a listening for client connections (in a separate thread)
    self.clientThread = threading.Thread(target=self._wait_for_clients_loop)
    self.clientThread.setDaemon(True) # script ends even if this thread is still running
    self.clientThread.start()
    
  def _wait_for_clients_loop(self):
    while True:
      # accept any new connection
      sockt, addr = self.serverSocket.accept()
      self.logger.info("Client connected %s:%d" % addr)
      self._clients.add((sockt, addr))
      
  #
  # Methods to manage user-provided frames
  #
  
  def getFrameCount(self):
    '''returns the total number of frames'''
    return self._frameCount
    
  def getCurrentFrame(self):
    '''returns the current frame'''
    return self._currentFrame
  
  def setCurrentFrame(self, frameNumber):
    '''Seeks to the current frame in the array of pre-loaded frames'''
    if (frameNumber >= 0 and frameNumber < self._frameCount):
      self._currentFrame = frameNumber
  
  def sendNextFrame(self):
    '''Sends the next frame to all clients connected.'''
    pass # implemented dynamically (python magic)
  
  def encodeAndSendJPEGToAllClients(self, rawFrame):
    '''Encodes and sends a JPEG to all connected clients'''
    jpg = self.encodeJPEG(rawFrame)     # creates JPEG
    self.sendJPEGToAllClients(jpg)                 # sends JPEG
  
  def sendJPEGToAllClients(self, jpg):
    '''Sends a JPEG to all connected clients'''
    removalSet = set()
    
    for client in self._clients:
      if self._sendJPEGToClient(client[0],jpg) < len(jpg):
        removalSet.add(client)
        
    for client in removalSet:
      self._clients.remove(client)
      self.logger.info("Client disconnected %s:%d" % client[1])
  
  def _sendNextFrameRaw(self):
    '''Sends the next frame to all clients.
       Calling this function will fail with an exception if the user didn't provide any frames'''
    self.encodeAndSendJPEGToAllClients(self._frames[self._currentFrame])
    self._currentFrame = (self._currentFrame + 1) % self._frameCount  
  
  def _sendNextFrameEncoded(self):
    '''Sends the next frame to all clients.
       Calling this function will fail with an exception if the user didn't provide any frames'''  
    self.sendJPEGToAllClients(self._encodedFrames[self._currentFrame])
    self._currentFrame = (self._currentFrame + 1) % self._frameCount

  def _sendJPEGToClient(self, socket, message):
    '''sends the entire frame to a client. Returns the number of bytes sent
       (if less than len(frame) then the client disconnected)'''
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
    
  #
  # Methods to start a streaming loop
  #
  
  def RunStreamingLoop(self):
  '''This **blocking** method loops until requested to stop through a CTRL+C or a Stop method'''
    self.loopForever = True
    maxTimePerFrame = (1.0 / self._fps) - 0.005
    while self.loopForever:
      try:
        startTime = time.time()         # check how long it takes to encode and stream frame
        self.sendNextFrame()            # sends the next frame to all clients
        remaingSleepTime = maxTimePerFrame - (time.time() - startTime)
        if remaingSleepTime > 0:
          time.sleep(remaingSleepTime)
        else:
          self.logger.info("We took too long (%f sec instead of %f sec)" % (maxTimePerFrame-remaingSleepTime,maxTimePerFrame))
        
      except KeyboardInterrupt:
        self.logger.info("CTRL+C requested! Stopping...")
        loopForever = False
      except Exception as e:
        self.logger.error("Unhandled exception!",e)
        loopForever = False
      

#
# ------------------------------------------------------------------------------------------------------------------------------------------------------
#


# When used as a script, JPEGTurboTCPServer allows users to stream jpegs, video files, and the user webcam
if __name__ == '__main__':
  #
  # Parsing script arguments
  #
  parser = argparse.ArgumentParser(description="TCPServer that streams a JPEG of current the current time")
  parser.add_argument('filename', type=str, help="file path that will be streamed over to all connected clients") 
  parser.add_argument('port', nargs='?', type=int, help="TCPServer's port (default=50000)", default=50000)  
  parser.add_argument('--webcam', nargs='?', action='store_const',const=True, default=False, help="if set, interprets the first argument as a webcam path / number", default=False)
  parser.add_argument('--quality', nargs='?', type=int, help="desired JPEG quality (default=90)", default=90)
  parser.add_argument('--fps', nargs='?', type=int, help="desired frame rate", default=30)
  
                    

  args = parser.parse_args()

  #
  # Setting up variables that we use in the rest of the script
  #

  PORT = args.port
  ADDR = "0.0.0.0"   
  QUALITY = args.quality
  FPS = args.fps

  mainLogger.info("Welcome to JPEGTurboTCPServer")
  mainLogger.info("Streaming %s (webcam? %s)" % (args.filename, "yes" if args.webcam else "no"))
  mainLogger.info("-"*20)

  # Opening video / file (the application will only crash if the user doesn't have OpenCV installed)
  import cv2
  import os
  
  opencvURI = parser.filename
  frames = None
  if (parser.webcam):
    try: 
        opencvURI = int(parser.filename) # if URI can be parsed to a number, then it is a webcam index
    except ValueError:
        opencvURI = parser.filename
  
  if parser.webcam:
    mainLogger.info("Opening file %s" % opencvURI)
  else:
    mainLogger.info("Opening webcam %s" % opencvURI)

  videoCapture = cv2.VideoCapture(opencvURI)
   
  # loop over the frames of the video to put them on an array
  frames = []
	while True:
		# grab the current frame
		(grabbed, frame) = video.read()
	 
		# check to see if we have reached the end of the
		# video
		if not grabbed:
			break
		frames.append(frame)

  #
  # Starting server
  #
  address = (ADDR, PORT)
  server = JPEGStreamerServer(address, frames, fps=FPS, quality=QUALITY)

  # listening loop (waits for new connections)  - runs in a separate thread
  server.ListenForClients()
  
  # are we streaming from a file? then we can let JPEGStreamerServer take over
  mainLogger.info("Streaming to clients! Use CTRL+C to stop...")
  if not parser.webcam:
    server.RunStreamingLoop()
  else:
    while True:
      try:
        ret, frame = cap.read()
        self.encodeAndSendJPEGToAllClients(frame)        
      except KeyboardInterrupt:
        self.logger.info("CTRL+C requested! Stopping...")
        loopForever = False
      except Exception as e:
        self.logger.error("Unhandled exception!",e)
        loopForever = False
      
  



