using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TCP Client for raw bytes[] implementing a basic Length-Value protocol
/// (This TCP Client is a stripped down version of https://github.com/WeibelLab/Comms-Unity)
/// 
/// Authors: Danilo Gaques, Tommy Sharkey
/// </summary>
/// 
public class TCPClientBehaviour : MonoBehaviour
{
    // Host
    [Tooltip("Name to use in the logs")]
    public string SocketName;

    [Header("TCPServer")]
    [Tooltip("TCPServer hostname")]
    public string HostName;
    [Tooltip("TCPServer port")]
    public int HostPort;

    [Tooltip("Check this box if the socket should connect when the script / game object is enabled / first starts")]
    public bool ConnectOnEnable = true;


    /// <summary>
    /// We raise this exception when the socket disconnects mid-read
    /// </summary>
    public class SocketDisconnected : Exception
    {
    }

    [Header("Events")]
    // dropping packets?
    [Tooltip("Data events will be called only with the last message received. Use wisely")]
    public bool dropAccumulatedMessages = false;

    // message handler
    public TCPClientSocketByteBufferReceived MessageReceivedEvent;

    // events
    public TCPClientSocketConnected OnConnect;
    public TCPClientSocketDisconnected OnDisconnect;
    private bool onConnectRaised = false;
    private bool onDisconnectRaised = false;

    // Thread variables
    [HideInInspector]
    public Queue<byte[]> messageQueue = new Queue<byte[]>();
    System.Object messageQueueLock = new System.Object();

    // Timeout
    [Header("Timeouts")]
    public int ReconnectTimeoutMs = 1000;
    public int ListenErrorTimeoutMs = 5000;



    [Header("Socket Statistics")]
    public double MessagesDropped = 0;
    public double MessagesReceived = 0;
    public double BytesReceived = 0;
    public double BytesDropped = 0;
    public double ConnectionErrors = 0;

    public DateTime LastConnectionStarted;
    public DateTime LastConnectionEnded;

    #region private members
    // TCP
    private TcpClient tcpClient;
    private bool killThreadRequested = false;

    // Shared
    private Thread listenerThread;
    private bool NeedToReconnect = false;

    // name used for the purpose of logging
    private string LogName;
    #endregion


    /// <summary>
    /// This boolean is true whenever a client is connected to the server
    /// or when the client is connected to a server
    /// </summary>
    bool _isConnected = false;
    public bool isConnected
    {
        get
        {
            return _isConnected;
        }
    }


    #region UnityEvents
    /// <summary>
    /// Called whenever the behavior is initialized by the application
    /// </summary>
    private void Awake()
    {
        if (SocketName.Length == 0)
            SocketName = this.gameObject.name;

        LogName = "[" + SocketName + " TCP Client] - ";
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // onConnectEvents should always come before any messages
        if (onConnectRaised)
        {
            _isConnected = true;

            if (OnConnect != null)
                OnConnect.Invoke(this);

            onConnectRaised = false;
        }

        // passess all the messages that are missing
        if (messageQueue.Count > 0)
        {
            // we should not spend time processing while the queue is locked
            // as this might disconnect the socket due to timeout
            Queue<byte[]> tmpQ;
            lock (messageQueueLock)
            {
                // copies the queue from the thread
                tmpQ = messageQueue;
                messageQueue = new Queue<byte[]>();
            }

            // now we can process our messages
            while (tmpQ.Count > 0)
            {
                // process message received
                byte[] msgBytes;
                msgBytes = tmpQ.Dequeue();

                // should we drop packets?
                while (dropAccumulatedMessages && tmpQ.Count > 0)
                {
                    msgBytes = tmpQ.Dequeue();

                    if (tmpQ.Count > 1)
                    { 
                        BytesDropped += msgBytes.Length;
                        ++MessagesDropped;
                    }

                }

                MessageReceivedEvent.Invoke(msgBytes);                
            }
        }

        // onDisconnectEvents should be passed after all messages are sent to clients
        if (onDisconnectRaised)
        {
            _isConnected = false;

            if (OnDisconnect != null)
                OnDisconnect.Invoke(this);

            onDisconnectRaised = false;
        }


    }

    private void OnEnable()
    {
        MessagesDropped = 0;
        MessagesReceived = 0;
        BytesReceived = 0;
        BytesDropped = 0;
        ConnectionErrors = 0;

        if (ConnectOnEnable)
            StartConnection();
    }

    private void OnDisable()
    {
        CloseConnection();
    }

    #endregion


    public string RemoteEndpointIP()
    {
        if (tcpClient != null)
        {
            return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
        }

        return "";
    }

    public int RemoteEndpointPort()
    {
        if (tcpClient != null)
        {
            return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
        }

        return -1;
    }

    /// <summary>
    /// Call this method when you want to start a connection (either listening as a server
    /// or connecting as a client).
    /// 
    ///  if `ConnectOnEnable` is checked / True, StartConnection will be called automatically for you ;)
    /// 
    /// </summary>
    public void StartConnection()
    {
        if (listenerThread != null && listenerThread.IsAlive)
        {
            Debug.LogWarning(LogName + "Already running. Call Disconnect() first or  ForceReconnect() instead");
            return;
        }
        killThreadRequested = false;

        try
        {
            listenerThread = new Thread(new ThreadStart(ClientLoopThread));
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError(LogName + " Failed to start socket thread: " + e);
        }
    }


    /// <summary>
    /// Closes the client connection (or the server)
    /// </summary>
    public void CloseConnection()
    {

        // Asks thread to stop listening
        killThreadRequested = true;

        // close all sockets
        try
        {
            if (tcpClient != null)
            {
                tcpClient.Close();
                tcpClient = null;
            }

        }
        catch (NullReferenceException)
        {
            // do nothing
        }


        // Is it connected? then update all members
        if (_isConnected)
        {
            _isConnected = false;
            onDisconnectRaised = false; // we are not sure that there will be another update loop

            LastConnectionEnded = DateTime.Now;

            // make sure others are aware that this socket disconnected
            if (OnDisconnect != null)
                OnDisconnect.Invoke(this);
        }

    }

    /// <summary>
    /// Restarts server / reconnects client
    /// </summary>
    public void ForceReconnect()
    {
        CloseConnection();
        StartConnection();
    }


    public void Send(string msg)
    {
        byte[] msgAsBytes = Encoding.UTF8.GetBytes(msg);
        Send(msgAsBytes);
    }

    public void Send(byte[] msg)
    {
        if (tcpClient == null)
        {
            Debug.LogWarning(LogName + "not connected! Dropping message...");
            return;
        }

        // Build message with Headers
        byte[] messageLength = BitConverter.GetBytes((UInt32)msg.Length);
        byte[] bytesToSend = new byte[msg.Length + sizeof(UInt32)];
        Buffer.BlockCopy(messageLength, 0, bytesToSend, 0, sizeof(UInt32));
        Buffer.BlockCopy(msg, 0, bytesToSend, sizeof(UInt32), msg.Length);


        // Send Message
        try
        {
            // Get a stream object for writing.
            NetworkStream stream = tcpClient.GetStream();
            
            // Send
            if (stream.CanWrite)
            {
                stream.Write(bytesToSend, 0, bytesToSend.Length);
            }
        }
        catch (SocketException e)
        {
            Debug.Log(LogName + " Socket Exception while sending: " + e);
        }
    }


    #region ReliableCommunication client/server implementation
    private void ClientLoopThread()
    {
        bool firstTime = true;
        bool socketConnected = false;

        while (!killThreadRequested)
        {
            try
            {
                socketConnected = false;
                if (!firstTime)
                    Thread.Sleep(ReconnectTimeoutMs);
                firstTime = false;

                Debug.Log(LogName + "Connecting to " + HostName + ":" + HostPort);
                tcpClient = new TcpClient(HostName, HostPort);
                Debug.Log(String.Format("{0} Connected to {1}:{2}", LogName, HostName, HostPort));
                LastConnectionStarted = DateTime.Now;
                socketConnected = true;
                onConnectRaised = true;
                //socketConnection.ReceiveBufferSize = 1024 * 1024; // 1 mb

                // handles messages
                SocketMessageReadingLoop(tcpClient);
            }
            catch (SocketException socketException)
            {
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.Interrupted:
                        return; // we were forcefully canceled - free thread
                    case SocketError.Shutdown:
                        return; // we forcefully freed the socket, so yeah, we will get an error
                    case SocketError.TimedOut:
                        if (killThreadRequested)
                            Debug.LogError(LogName + "timed out");
                        else
                            Debug.LogError(LogName + "timed out - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                        ++ConnectionErrors;
                        break;
                    case SocketError.ConnectionRefused:
                        if (killThreadRequested)
                            Debug.LogError(LogName + "connection refused! Are you sure the server is running?");
                        else
                            Debug.LogError(LogName + "connection refused! Are you sure the server is running? - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                        ++ConnectionErrors;
                        break;
                    case SocketError.NotConnected:
                        // this sounds extra, but sockets that never connected will die with NotConnected
                        if (socketConnected)
                        {
                            Debug.LogError(LogName + " Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                            ++ConnectionErrors;
                        }
                        break;
                    default:
                        // if we didn't interrupt it -> reconnect, report statistics, log warning
                        Debug.LogError(LogName + " Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                        ++ConnectionErrors;
                        break;
                }

            }
            catch (ObjectDisposedException)
            {
                // this exception happens when the socket could not finish  its operation
                // and we forcefully aborted the thread and cleared the object
            }
            catch (ThreadAbortException)
            {
                // this exception happens when the socket could not finish  its operation
                // and we forcefully aborted the thread (we wait 100 ms)
            }
            catch (SocketDisconnected)
            {
                // this is our very own exception for when a client disconnects during a read
                // do nothing.. finally will take care of it below
            }
            catch (Exception e)
            {
                // this is likely not a socket error. So while we do not record a stream error,
                // we still log for later learning about it
                Debug.LogWarning(LogName + "Exception " + e);
            }
            finally
            {
                if (socketConnected)
                {
                    if (killThreadRequested)
                        Debug.Log(LogName + "Disconnected");
                    else
                        Debug.Log(LogName + "Disconnected - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                    LastConnectionEnded = DateTime.Now;
                    onDisconnectRaised = true;
                }
            }
        }
    }

   private void SocketMessageReadingLoop(TcpClient tcpClientSocket, bool incoming = false)
   {
        using (NetworkStream stream = tcpClientSocket.GetStream())
        {
            try
            {
                    byte[] lengthHeader = new byte[4];
                    while (!killThreadRequested)
                    {
                        // reads 4 bytes - header
                        readToBuffer(stream, lengthHeader, lengthHeader.Length);

                        // convert to int (UInt32LE)
                        UInt32 msgLength = BitConverter.ToUInt32(lengthHeader, 0);

                        // create appropriately sized byte array for message
                        byte[] bytes = new byte[msgLength];

                        // create appropriately sized byte array for message
                        bytes = new byte[msgLength];
                        readToBuffer(stream, bytes, bytes.Length);

                        ++MessagesReceived;
                        lock (messageQueueLock)
                        {
                            messageQueue.Enqueue(bytes);
                        }
                    }

            }
            catch (System.IO.IOException ioException)
            {
                // when stream read fails, it throws IOException.
                // let's expose that exception and handle it below
                throw ioException.InnerException;
            }
        }
    }


    /// <summary>
    /// Reads readLength bytes from a network stream and saves it to buffer
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="readLength"></param>
    void readToBuffer(NetworkStream stream, byte[] buffer, int readLength)
    {
        int offset = 0;
        // keeps reading until a full message is received
        while (offset < buffer.Length)
        {
            int bytesRead = stream.Read(buffer, offset, readLength - offset); // read from stream
            ++BytesReceived;

            // "  If the remote host shuts down the connection, and all available data has been received,
            // the Read method completes immediately and return zero bytes. "
            // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream.read?view=netframework-4.0
            if (bytesRead == 0)
            {
                throw new SocketDisconnected();// returning here means that we are done
            }

            offset += bytesRead; // updates offset
        }
    }


    #endregion

}

[System.Serializable]
public class TCPClientSocketConnected : UnityEvent<TCPClientBehaviour> { }

[System.Serializable]
public class TCPClientSocketDisconnected : UnityEvent<TCPClientBehaviour> { }

[System.Serializable]
public class TCPClientSocketByteBufferReceived : UnityEvent<byte[]> { }
