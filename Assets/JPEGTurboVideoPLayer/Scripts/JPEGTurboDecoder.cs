using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/**
 * JPEGTurboDecoder decodes a JPEG into an RGB byte stream.
 * 
 * --> Using JPEGTurboDecoder is quite simple:
 * 
 * // assuming here that myJpegFileBuffer is a byte[] with your jpeg
 * JPEGTurboDecoder decoder = new JPEGTurboDecoder(); // instantiate it
 * if (decoder.Available()) {  // check that the decoder is available
 *    int width, int height;
 *    decoder.DecodeJPEGHeader(myJpegFileBuffer, width, height); // gets jpeg size
 *    
 *    byte[] decodedJpeg = new byte[width * height * 3]; // aiming for RGB (default)
 *    
 *    decoder.DecodeJPEG(myJpegFileBuffer, width, height, decodedJpeg);
 *    
 *    // done
 *    (ideally, you can dispose of the decoder when you are done using it)
 *    decoder.Dispose();
 * 
 * --> author: Danilo Gasques (gasques@ucsd.edu)
 */
public class JPEGTurboDecoder : IDisposable
{
    // static method that checks if the library is available
    IntPtr decoderHandle;
    private bool decoderAvailable = false;
    private bool disposedValue = false;    // IDisposables have to be disposed


    // Decoder specific
    int decodingFlags = LibJPEGTurbo.TJFLAG_ACCURATEDCT | LibJPEGTurbo.TJFLAG_BOTTOMUP;


    /** Use the fastest DCT/IDCT algorithm available in the underlying codec
     * The libjpeg implementation, for example, uses the fast algorithm by
     * default when compressing, because this has been shown to have only a very slight effect
     * on accuracy, but it uses the accurate algorithm when decompressing, because this has been 
     * shown to have a larger effect.
     */
    public bool FastDCT
    {
        set
        {
            if (value)
            {
                // first, disable accurate decoding if enabled
                decodingFlags = decodingFlags & (int.MaxValue ^ LibJPEGTurbo.TJFLAG_ACCURATEDCT);

                // second, enable  LibJPEGTurbo.TJFLAG_FASTDCT
                decodingFlags = decodingFlags | LibJPEGTurbo.TJFLAG_FASTDCT;
            }
            else
            {
                // first, disable TJFLAG_FASTDCT
                decodingFlags = decodingFlags & (int.MaxValue ^ LibJPEGTurbo.TJFLAG_FASTDCT);

                // second, enable TJFLAG_ACCURATEDCT
                decodingFlags = decodingFlags | LibJPEGTurbo.TJFLAG_ACCURATEDCT;
            }
        }

        get
        {
            return (LibJPEGTurbo.TJFLAG_FASTDCT & decodingFlags) > 0;
        }
    }
    
    public JPEGTurboDecoder()
    {
        try
        {
            decoderHandle = LibJPEGTurbo.tjInitDecompress();
            decoderAvailable = true;

            if (decoderHandle == IntPtr.Zero)
            {
                // LibJPEGTurbo failed (Todo: report why it failed)
                decoderAvailable = false;
            }
        } catch (Exception e)
        {
            Debug.LogError(string.Format("[JPEGTurboDecoder] Could not start instantiate decoder: {0}", e));
            decoderAvailable = false;
        }
    }

    // Decodes a JPEG header. Returns false if decoder is not available or buffer doesn't contain a valid JPEG
    public bool DecodeJPEGHeader(byte[] buffer, ref int width, ref int height)
    {
        width = 0;
        height = 0;
        int jpegSubsample = 0;

        // try decoding
        if (decoderAvailable &&
            LibJPEGTurbo.tjDecompressHeader2(decoderHandle, buffer, buffer.LongLength, ref width, ref height, ref jpegSubsample) == 0)
        {
            return true;
        }
        
        return false;
    }

    // Decodes a JPEG on a user provided buffer
    public bool DecodeJPEG(byte[] compressedBuffer, int width, int height, byte[] decompressedBuffer, LibJPEGTurbo.TJPF decodeTo = LibJPEGTurbo.TJPF.TJPF_RGB)
    {
        if (decoderAvailable &&
            LibJPEGTurbo.tjDecompress2(decoderHandle, 
            compressedBuffer, compressedBuffer.LongLength,
            decompressedBuffer, width, 0, height,
            (int)decodeTo, decodingFlags) == 0)
        {
            return true;
        }
        return false;
    }

    public bool Available()
    {
        return decoderAvailable;
    }


    #region IDisposable implementation - calls tjDestroy
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            // let others know this is gone
            disposedValue = true;


            // // frees managed resources (we have none)
            //if (disposing)
            //{
            // TODO: dispose managed state (managed objects)
            // nothing here
            //}

            // frees LigJPEGTurbo handle
            decoderAvailable = false;

            // did we have a decoder available?
            if (decoderHandle != IntPtr.Zero)
                LibJPEGTurbo.tjDestroy(decoderHandle);

            // zero decoder
            decoderHandle = IntPtr.Zero;

        }
    }

     // C# finalizer to get rid of unmanaged resources
     ~JPEGTurboDecoder()
     {
         // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
         Dispose(disposing: false);
     }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
