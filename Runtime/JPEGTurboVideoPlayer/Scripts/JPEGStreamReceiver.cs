using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace xrcollabtk
{
    [Serializable]
    public enum scaleMeshAxis
    {
        X,
        Y,
        Z
    }

    /**
     *
     * JPEGStreamReceiver uses JPEGTurbo to decode jpeg's in a separate thread, updating a Texture on Unity whenever possible
     *
     * How to use it?
     * - Add JPEGStreamReceiver to a Transform (e.g.: plane)
     * -- Associate a MeshRenderer with MeshToUpdate
     * - From another script, invoke NewFrameArrived to update the texture associated with MeshRenderer
     * 
     * - Scaling the mesh to match the JPEG's aspect ratio:
     * 
     *   if the property `scaleMesh` is true, then this script will automatically rescale
     * the transform holding MeshToUpdate so that its aspect ratio matches the last JPEG
     * decoded.
     * 
     *   Note: scaling the transform will never exced the original dimensions given to
     *   the transform. The current approach only scales down one of the dimensions (either
     *   the one representing width or height) depending on the **original size**
     *
     * History:
     * - v1.0.2:
     * -- Using an array of meshs to update (instead of a single mesh)
     * 
     * - v1.0.1: 
     * -- scale mesh: scales the transform holding MeshRenderer to match texture aspect ratio.
     * 
     * - v1.0.0: first version
     *
     * author: Danilo Gasques (gasques@ucsd.edu)
     */
    public class JPEGStreamReceiver : MonoBehaviour
    {

        [Serializable]
        public class MeshToUpdate
        {

            [Tooltip("If true, it will scale the mesh so that it has the same aspect ratio as the JPEG being decoded")]
            public bool scaleMesh = false;
            [Tooltip("Axis that can be scale to match texture aspect ratio (horizontal / width axis)")]
            public scaleMeshAxis widthAxis = scaleMeshAxis.X;
            [Tooltip("Axis that can be scale to match texture aspect ratio (vertical / height axis)")]
            public scaleMeshAxis heightAxis = scaleMeshAxis.Y;
            [Tooltip("MeshRenderer that this script will update - if not set, JPEGStreamReceiver looks for a MeshRenderer in the same transform")]
            public MeshRenderer mesh;

            public float originalWidthAxisScale, originalHeightAxisScale;

            public MeshToUpdate()
            {
                scaleMesh = false;
                widthAxis = scaleMeshAxis.X;
                heightAxis = scaleMeshAxis.Y;
                originalWidthAxisScale = 1.0f;
                originalHeightAxisScale = 1.0f;
            }

        /// Changes the scale dimension on a single axis (X,Y, or Z)
        public void SetAxisDimension(scaleMeshAxis axis, float scale)
            {
                if (mesh != null && mesh.transform != null)
                { 
                    Vector3 newScale = mesh.transform.localScale;
                    switch (axis)
                    {
                        case scaleMeshAxis.X:
                            newScale.x = scale;
                            break;

                        case scaleMeshAxis.Y:
                            newScale.y = scale;
                            break;

                        case scaleMeshAxis.Z:
                            newScale.z = scale;
                            break;
                    }
                    mesh.transform.localScale = newScale;
                }
            }

            /// Gets the scale dimension on a single axis (X,Y, or Z)
            public float GetAxisDimension(scaleMeshAxis axis)
            {
                if (mesh != null && mesh.transform != null)
                {
                    switch (axis)
                    {
                        case scaleMeshAxis.X:
                            return mesh.transform.localScale.x;

                        case scaleMeshAxis.Y:
                            return mesh.transform.localScale.y;

                        case scaleMeshAxis.Z:
                            return mesh.transform.localScale.z;
                    }
                    return 1.0f;
                }
                return 1.0f;
            }
        };

        [Header("Mesh and Texture")]
        [Tooltip("MeshRenderer that this script will update - if not set, JPEGStreamReceiver looks for a MeshRenderer in the same transform")]
        public MeshToUpdate[] meshesToUpdate;

        //[Tooltip("If UpdateTexture is true, then the associated MeshRenderer will have its texture updated when a JPEG is decoded")]
        //public bool UpdateTexture = true;

        // if true, it means that our texture was set on a MeshRenderer
        private bool SetTexture = false;

        #region Private info related to JPEG decoding and textures to display JPEG
        Texture2D videoTexture;
        public int textureWidth, textureHeight;

        // receives all encoded jpegs in a queue so that it can decode them
        Queue<byte[]> encodedFramesQ = new Queue<byte[]>();
        System.Object encodedFramesQLock = new System.Object();

        // decoding thread uses one pointer and the display thread uses another one
        FrameSettings currentDisplayBuffer = new FrameSettings();
        FrameSettings currentDecodeBuffer = new FrameSettings();

        // finally, we use locks to switch between modes
        System.Object frameSwapLock = new System.Object();

        #endregion

        [Tooltip("libjpeg-turbo is already fast, but it can run faster! Notice: Faster decoding can causes quality drops")]
        public bool FasterDecoding = false;

        // keeps track of how many frames were dropped due to 
        // not being able to display them
        long droppedFrames = 0, decodedFrameErrors = 0, decodedFrames = 0, displayedFrames;
        string LogName = "";

        #region Decoder Thread
        /// <summary>
        /// Whenever LibJPEGTurbo is available, we use a thread to decode
        /// (despite the libjpeg-turbo being fast, it still takes cycles from the render loop)
        /// </summary>
        Thread jpegDecoderThread;
        bool decoderThreadRunning = false;

        #endregion

        JPEGTurboDecoder jpegDecoder;

        #region Unity Events

        DateTime onenabletime;
        public void OnEnable()
        {
            LogName = this.name;

            droppedFrames = 0;
            decodedFrameErrors = 0;
            decodedFrames = 0;
            displayedFrames = 0;

            if (videoTexture == null)
            {
                videoTexture = new Texture2D(1, 1);

                // no meshes set? then we define an array with a single mesh with the one attached to this object (if we have one)
                if (meshesToUpdate == null && GetComponent<MeshRenderer>() != null)
                {
                    meshesToUpdate = new MeshToUpdate[0];
                    meshesToUpdate[0].mesh = GetComponent<MeshRenderer>();
                    meshesToUpdate[0].scaleMesh = true;
                }

                // no meshes at all?
                if (meshesToUpdate == null)
                {
                    Debug.LogError(string.Format("[JPEGStreamReceiver@{0}] Could not find a suitable MeshRenderer... Make sure this script is added to an object that has a mesh", LogName));
                }
                else
                {
                    Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Updating a total of {1} mesh(es) with JPEGS", LogName, meshesToUpdate.Length));
                    foreach (MeshToUpdate m in meshesToUpdate)
                    {
                        if (m.mesh != null)
                        { 
                          // sets video mesh
                          m.mesh.material.mainTexture = videoTexture;

                          // saves original scale dimensions to scale it back
                          m.originalWidthAxisScale = m.GetAxisDimension(m.widthAxis);
                          m.originalHeightAxisScale = m.GetAxisDimension(m.heightAxis);
                        }
                    }                    

                    SetTexture = true;
                }


            }

            // creates decoder
            jpegDecoder = new JPEGTurboDecoder();

            // tries to allocate decoder
            if (jpegDecoder.Available())
            {
                // are we decoding as fast as possible?
                jpegDecoder.FastDCT = FasterDecoding;

                // let's get the decoder thread started!
                decoderThreadRunning = true;
                try
                {
                    jpegDecoderThread = new Thread(DecoderThread);
                    jpegDecoderThread.IsBackground = true;
                    jpegDecoderThread.Start();
                }
                catch (Exception e)
                {
                    decoderThreadRunning = false;
                    Debug.LogError(string.Format("[JPEGStreamReceiver@{0}] Could not start LibJPEGTurbo Decoder Thread! Using Unity's built-in decoder (slow)...!{1}", LogName, e));
                    if (jpegDecoderThread != null && jpegDecoderThread.IsAlive)
                    {
                        jpegDecoderThread.Abort();
                    }

                    jpegDecoderThread = null;
                }

            }
            else
            {
                Debug.LogError(string.Format("[JPEGStreamReceiver@{0}] Could not start LibJPEGTurbo! Using Unity's built-in decoder (slow)...!", LogName));
            }


            onenabletime = DateTime.Now;
        }

        public void OnDisable()
        {
            // is thread running ? stops it first
            if (jpegDecoderThread != null)
            {
                decoderThreadRunning = false;
                // Todo: signal conditional variable

                // are we still running?
                if (jpegDecoderThread.IsAlive)
                {
                    try
                    {

                        lock (encodedFramesQLock)
                        {
                            // forces the end of this
                            Monitor.Pulse(encodedFramesQLock);
                        }

                        // waits for 200ms (it shouldn't take that much)
                        jpegDecoderThread.Join(200);

                        // too bad, we are stil running
                        if (jpegDecoderThread.IsAlive)
                        {
                            jpegDecoderThread.Abort();
                        }


                    }
                    catch (Exception)
                    {
                        // don't care
                    }

                    jpegDecoderThread = null;
                }
            }

            // frees decoders and buffers
            if (jpegDecoder != null)
            {
                jpegDecoder.Dispose();
                jpegDecoder = null;
            }

            if (droppedFrames > 0)
            {
                Debug.LogWarning(string.Format("[JPEGStreamReceiver@{0}] Dropped a total of {1} frames", LogName, droppedFrames));
            }

            // time so far
            double totalTimePlaying = (DateTime.Now - onenabletime).TotalSeconds;
            Debug.LogWarning(string.Format("[JPEGStreamReceiver@{0}] Stats: {1} frames displayed, {2} frames dropped, {3} frames decoded. Displayed at {4} fps. Decoded at {5} fps.", LogName, displayedFrames, droppedFrames, decodedFrames, displayedFrames / totalTimePlaying, decodedFrames / totalTimePlaying));

        }

        byte[] currentBuffer = null;
        int currentWidth, currentHeight;
        private void Update()
        {
            // if we are using the libjpeg-turbo decoder and we have a frame ready
            if (decoderThreadRunning)
            {
                // do we have a frame available?
                lock (frameSwapLock)
                {
                    // yes!
                    if (currentDisplayBuffer.dirty)
                    {
                        currentBuffer = currentDisplayBuffer.buffer;
                        currentWidth = currentDisplayBuffer.bufferWidth;
                        currentHeight = currentDisplayBuffer.bufferHeight;
                    }

                    // let the decoder know we are done with this buffer
                    currentDisplayBuffer.buffer = null;
                    currentDisplayBuffer.dirty = false;
                }

                // did we get anything?
                if (currentBuffer != null)
                {
                    // display the frame
                    // did the texture size change?
                    if (currentWidth != textureWidth || currentHeight != textureHeight)
                    {

                        textureWidth = currentDisplayBuffer.bufferWidth;
                        textureHeight = currentDisplayBuffer.bufferHeight;

                        // we have to create a new videoTexture
                        if (videoTexture != null)
                        {
                            // creates new video texture
                            videoTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);

                            // do we have any meshes we want to update?
                            if (meshesToUpdate != null)
                            {
                                foreach (MeshToUpdate m in meshesToUpdate)
                                {
                                    // updates video mesh
                                    if (m.mesh != null)
                                      m.mesh.material.mainTexture = videoTexture;
                                }
                            }
                            // we set the texture
                            SetTexture = true;
                        }

                        // let everybody know that we have a new video texture
                        Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Allocated a texture buffer of {1}x{2}", LogName, textureWidth, textureHeight));

                        // we have to resize meshes to keep up with aspect ratio
                        if (meshesToUpdate != null)
                        {
                            foreach (MeshToUpdate m in meshesToUpdate)
                            {
                                if (m.mesh != null && m.scaleMesh)
                                { 
                                    // does the aspect ratio differ by any chance?
                                    float aspectRatio = ((float)(textureWidth)) / ((float)(textureHeight));
                                    float displayAspectRatio = m.originalWidthAxisScale / m.originalHeightAxisScale;

                                    // aspect ratio doesn't match? scale panel to fill the texture on one of the dimensions
                                    if (Math.Abs(displayAspectRatio - aspectRatio) > 0.001f)
                                    {
                                        if (displayAspectRatio > aspectRatio) // 
                                        {
                                            // fills display height and scales down width
                                            m.SetAxisDimension(m.widthAxis, aspectRatio * m.originalHeightAxisScale);

                                        }
                                        else // displayAspectRatio < aspectRatio
                                        {
                                            m.SetAxisDimension(m.heightAxis, (1.0f / aspectRatio) * m.originalWidthAxisScale);
                                        }

                                        Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Scalled mesh in \"{1}\" from size {2}x{3} to {4}x{5}", LogName, m.mesh.transform.name, m.originalWidthAxisScale, m.originalHeightAxisScale, m.GetAxisDimension(m.widthAxis), m.GetAxisDimension(m.heightAxis)));
                                    }
                                }

                                
                            }
                        }
                        
                    }

                    // upload texture to the GPU
                    videoTexture.LoadRawTextureData(currentBuffer);
                    videoTexture.Apply();

                    // increments frames played
                    displayedFrames++;

                    // frees it before next render loop
                    currentBuffer = null;
                }
            }


        }

        #endregion

        public void DecoderThread()
        {
            Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Started decoder thread", LogName));
            while (decoderThreadRunning)
            {
                try
                {
                    bool decodeError = false;
                    // processes the next frame
                    byte[] frame = new byte[1];

                    // real thread loop is this one. The external try catch just makes sure that we will restart no matter what
                    while (decoderThreadRunning)
                    {

                        Queue<byte[]> tmpFrameQ = new Queue<byte[]>();
                        lock (encodedFramesQLock)
                        {
                            Monitor.Wait(encodedFramesQLock); // waits for a pulse so that we know that we can dequeue

                            // were we killed?
                            if (!decoderThreadRunning)
                            {
                                break;
                            }

                            // not killed! Do we have a frame?
                            if (encodedFramesQ.Count > 0)
                            {
                                tmpFrameQ = encodedFramesQ;
                                encodedFramesQ = new Queue<byte[]>();
                            }

                        }

                        while (decoderThreadRunning && tmpFrameQ.Count > 0)
                        {

                            // skips frames that we might have accumulated over time (frame will have the last frame)
                            while (tmpFrameQ.Count > 1)
                            {
                                ++droppedFrames;
                                frame = tmpFrameQ.Dequeue();
                            }

                            // gets the lastest frame
                            frame = tmpFrameQ.Dequeue();

                            // while we have frames, decode them
                            int width = 0, height = 0;

                            decodeError = false;
                            // decodes headers and allocates more memory if needed
                            if (jpegDecoder != null && jpegDecoder.DecodeJPEGHeader(frame, ref width, ref height))
                            {
                                if (currentDecodeBuffer.buffer == null || width != currentDecodeBuffer.bufferWidth || height != currentDecodeBuffer.bufferHeight)
                                {

                                    // rgb
                                    currentDecodeBuffer.buffer = null;
                                    currentDecodeBuffer.buffer = new byte[width * height * 3];
                                    currentDecodeBuffer.dirty = false; // won't let anyone read it for now
                                    currentDecodeBuffer.bufferWidth = (int)width;
                                    currentDecodeBuffer.bufferHeight = (int)height;

                                    // Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Allocated a buffer of {1}x{2}", LogName, width, height));
                                }

                                // decodes JPEG to RGB
                                if (jpegDecoder != null && jpegDecoder.DecodeJPEG(frame, currentDecodeBuffer.bufferWidth, currentDecodeBuffer.bufferHeight, currentDecodeBuffer.buffer, LibJPEGTurbo.TJPF.TJPF_RGB))
                                {
                                    // increment decoder counter
                                    ++decodedFrames;

                                    // decoded successfully, so we can let others display this frame
                                    currentDecodeBuffer.dirty = true;

                                    // done decoding on this buffer, time to swap
                                    lock (frameSwapLock)
                                    {
                                        FrameSettings tmp = currentDisplayBuffer;

                                        // frame in the display buffer wasn't displayed.. that means that we dropped it
                                        if (currentDisplayBuffer.dirty)
                                        {
                                            ++droppedFrames;
                                        }

                                        // prepare to display this buffer
                                        currentDisplayBuffer = currentDecodeBuffer;

                                        // prepare to decode on another buffer
                                        currentDecodeBuffer = tmp;
                                    }
                                }
                                else
                                {
                                    decodeError = true;
                                }
                            }
                            else
                            {
                                decodeError = true;
                            }

                            // decodes JPEG
                            if (decodeError)

                                // shows the message once every 120 frames (4 seconds)
                                if (decodedFrameErrors % 120 == 0)
                                {
                                    Debug.LogError(string.Format("[JPEGStreamReceiver@{0}] Unable to decode JPEG (Total decode errors so far {1})", LogName, decodedFrameErrors + 1));
                                }

                            // if decoding failed
                            decodedFrameErrors += 1;
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("[JPEGStreamReceiver@{0}] Decoder thread threw an exception: {1}", LogName, e));
                }
            }
            Debug.Log(string.Format("[JPEGStreamReceiver@{0}] Decoder thread exitted successfully", LogName));

            // if we have any pending frames, we should add them do the droppedFrames
            droppedFrames += encodedFramesQ.Count;
        }

        /// <summary>
        /// Invoke to have the resulting JPEG decoded in a separate thread (when libjpeg turbo)
        /// </summary>
        /// <param name="frame"></param>
        /// 
        public void NewFrameArrived(byte[] frame)
        {
            if (frame != null)
            {
                // libjpeg decoder is available?
                if (jpegDecoder != null && jpegDecoder.Available())
                {
                    if (decoderThreadRunning)
                    {
                        lock (encodedFramesQLock)
                        {
                            // adds a frame to the queue
                            encodedFramesQ.Enqueue(frame);

                            // tells others to get the frame
                            Monitor.Pulse(encodedFramesQLock);
                        }
                    }
                    else
                    {
                        // dropping frames because thread is not running
                        ++droppedFrames;
                    }
                }
                else
                {
                    try
                    {
                        // fallback to unity's decoder (it supports other textures)
                        videoTexture.LoadImage(frame);
                        ++displayedFrames;
                        ++decodedFrames;
                    }
                    catch (Exception)
                    {
                        ++decodedFrameErrors;
                    }

                    // adjusts the screen size
                    if (!SetTexture)
                    {
                        //GetComponentInParent<AdaptiveSizing>().Resize();
                        SetTexture = true;
                    }
                }

            }
        }




        #region FrameSettings definition
        /// <summary>
        /// FrameSettings works as a temporary buffer for frames decoded by JPEGStreamReceiver
        /// </summary>
        class FrameSettings
        {
            /// <summary>
            /// frame width and height
            /// </summary>
            public int bufferWidth = -1, bufferHeight = -1;

            /// <summary>
            /// True if a new frame was decoded into buffer. If false, buffer might be empty or with
            /// an invalid frame
            /// </summary>
            public bool dirty = false;

            /// <summary>
            /// RGB buffer
            /// </summary>
            public byte[] buffer;
        }
        #endregion
    }


}