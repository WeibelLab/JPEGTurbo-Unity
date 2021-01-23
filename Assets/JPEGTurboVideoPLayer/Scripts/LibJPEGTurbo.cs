
using System;
using System.Runtime.InteropServices;

/// <summary>
/// This class wraps lipjpegturbo.
/// You should not use it directly unless you want something specific from Turbo JPEG
/// If you are interested in decoding or encoding JPEGs, consider the following classes:
/// 
/// Basic classes:
/// - JPEGTurboDecoder: jpeg decoder wrap
/// - (todo) JPEGTurboEncoder: jpeg encoder wrap
/// 
/// Application-specific classes:
/// - JPEGStreamReceiver: receives a JPEG over TCP and decodes it
/// - (todo) JPEGSTreamSender: encodes a byte stream and sends it as a JPEG over TCP
/// 
/// Author: danilo gasques - gasques@ucsd.edu
/// </summary>
public class LibJPEGTurbo
{
    
    private const string NATIVE_LIBRARY_NAME = "turbojpeg";
    /**
    * The uncompressed source/destination image is stored in bottom-up (Windows,
    * OpenGL) order, not top-down (X11) order.
    */
    public const int TJFLAG_BOTTOMUP = 2;
    /**
     * Turn off CPU auto-detection and force TurboJPEG to use MMX code (if the
     * underlying codec supports it.)
     */
    public const int TJFLAG_FORCEMMX = 8;
    /**
     * Turn off CPU auto-detection and force TurboJPEG to use SSE code (if the
     * underlying codec supports it.)
     */
    public const int TJFLAG_FORCESSE = 16;
    /**
     * Turn off CPU auto-detection and force TurboJPEG to use SSE2 code (if the
     * underlying codec supports it.)
     */
    public const int TJFLAG_FORCESSE2 = 32;
    /**
     * Turn off CPU auto-detection and force TurboJPEG to use SSE3 code (if the
     * underlying codec supports it.)
     */
    public const int TJFLAG_FORCESSE3 = 128;
    /**
     * When decompressing an image that was compressed using chrominance
     * subsampling, use the fastest chrominance upsampling algorithm available in
     * the underlying codec.  The default is to use smooth upsampling, which
     * creates a smooth transition between neighboring chrominance components in
     * order to reduce upsampling artifacts in the decompressed image.
     */
    public const int TJFLAG_FASTUPSAMPLE = 256;
    /**
     * Disable buffer (re)allocation.  If passed to #tjCompress2() or
     * #tjTransform(), this flag will cause those functions to generate an error if
     * the JPEG image buffer is invalid or too small rather than attempting to
     * allocate or reallocate that buffer.  This reproduces the behavior of earlier
     * versions of TurboJPEG.
     */
    public const int TJFLAG_NOREALLOC = 1024;
    /**
     * Use the fastest DCT/IDCT algorithm available in the underlying codec.  The
     * default if this flag is not specified is implementation-specific.  For
     * example, the implementation of TurboJPEG for libjpeg[-turbo] uses the fast
     * algorithm by default when compressing, because this has been shown to have
     * only a very slight effect on accuracy, but it uses the accurate algorithm
     * when decompressing, because this has been shown to have a larger effect.
     */
    public const int TJFLAG_FASTDCT = 2048;
    /**
     * Use the most accurate DCT/IDCT algorithm available in the underlying codec.
     * The default if this flag is not specified is implementation-specific.  For
     * example, the implementation of TurboJPEG for libjpeg[-turbo] uses the fast
     * algorithm by default when compressing, because this has been shown to have
     * only a very slight effect on accuracy, but it uses the accurate algorithm
     * when decompressing, because this has been shown to have a larger effect.
     */
    public const int TJFLAG_ACCURATEDCT = 4096;


    public enum TJPF
    {
        /**
         * RGB pixel format.  The red, green, and blue components in the image are
         * stored in 3-byte pixels in the order R, G, B from lowest to highest byte
         * address within each pixel.
         */
        TJPF_RGB = 0,
        /**
         * BGR pixel format.  The red, green, and blue components in the image are
         * stored in 3-byte pixels in the order B, G, R from lowest to highest byte
         * address within each pixel.
         */
        TJPF_BGR,
        /**
         * RGBX pixel format.  The red, green, and blue components in the image are
         * stored in 4-byte pixels in the order R, G, B from lowest to highest byte
         * address within each pixel.  The X component is ignored when compressing
         * and undefined when decompressing.
         */
        TJPF_RGBX,
        /**
         * BGRX pixel format.  The red, green, and blue components in the image are
         * stored in 4-byte pixels in the order B, G, R from lowest to highest byte
         * address within each pixel.  The X component is ignored when compressing
         * and undefined when decompressing.
         */
        TJPF_BGRX,
        /**
         * XBGR pixel format.  The red, green, and blue components in the image are
         * stored in 4-byte pixels in the order R, G, B from highest to lowest byte
         * address within each pixel.  The X component is ignored when compressing
         * and undefined when decompressing.
         */
        TJPF_XBGR,
        /**
         * XRGB pixel format.  The red, green, and blue components in the image are
         * stored in 4-byte pixels in the order B, G, R from highest to lowest byte
         * address within each pixel.  The X component is ignored when compressing
         * and undefined when decompressing.
         */
        TJPF_XRGB,
        /**
         * Grayscale pixel format.  Each 1-byte pixel represents a luminance
         * (brightness) level from 0 to 255.
         */
        TJPF_GRAY,
        /**
         * RGBA pixel format.  This is the same as @ref TJPF_RGBX, except that when
         * decompressing, the X component is guaranteed to be 0xFF, which can be
         * interpreted as an opaque alpha channel.
         */
        TJPF_RGBA,
        /**
         * BGRA pixel format.  This is the same as @ref TJPF_BGRX, except that when
         * decompressing, the X component is guaranteed to be 0xFF, which can be
         * interpreted as an opaque alpha channel.
         */
        TJPF_BGRA,
        /**
         * ABGR pixel format.  This is the same as @ref TJPF_XBGR, except that when
         * decompressing, the X component is guaranteed to be 0xFF, which can be
         * interpreted as an opaque alpha channel.
         */
        TJPF_ABGR,
        /**
         * ARGB pixel format.  This is the same as @ref TJPF_XRGB, except that when
         * decompressing, the X component is guaranteed to be 0xFF, which can be
         * interpreted as an opaque alpha channel.
         */
        TJPF_ARGB,
        /**
         * CMYK pixel format.  Unlike RGB, which is an additive color model used
         * primarily for display, CMYK (Cyan/Magenta/Yellow/Key) is a subtractive
         * color model used primarily for printing.  In the CMYK color model, the
         * value of each color component typically corresponds to an amount of cyan,
         * magenta, yellow, or black ink that is applied to a white background.  In
         * order to convert between CMYK and RGB, it is necessary to use a color
         * management system (CMS.)  A CMS will attempt to map colors within the
         * printer's gamut to perceptually similar colors in the display's gamut and
         * vice versa, but the mapping is typically not 1:1 or reversible, nor can it
         * be defined with a simple formula.  Thus, such a conversion is out of scope
         * for a codec library.  However, the TurboJPEG API allows for compressing
         * CMYK pixels into a YCCK JPEG image (see #TJCS_YCCK) and decompressing YCCK
         * JPEG images into CMYK pixels.
         */
        TJPF_CMYK
    };


    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr tjInitDecompress();

    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr tjInitCompress();

    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern int tjCompress2(IntPtr handle, byte[] srcBuf, int width, int pitch, int height, int pixelFormat, ref IntPtr jpegBuf, ref long _jpegSize, int jpegSubsamp, int jpegQual, int flags);


    /// <summary>
    /// The maximum size of the buffer (in bytes) required to hold a JPEG image with the given parameters.
    /// The number of bytes returned by this function is larger than the size of the uncompressed source image.
    /// The reason for this is that the JPEG format uses 16-bit coefficients, and it is thus possible for a very high-quality
    /// JPEG image with very high-frequency content to expand rather than compress when converted to the JPEG format.
    /// Such images represent a very rare corner case, but since there is no way to predict the size of a JPEG image prior
    /// to compression, the corner case has to be handled.
    /// </summary>
    /// <param name="width">width of the image (in pixels)</param>
    /// <param name="height">height of the image (in pixels)</param>
    /// <param name="jpegSubsamp">the level of chrominance subsampling to be used when generating the JPEG image</param>
    /// <returns>the maximum size of the buffer (in bytes) required to hold the image, or -1 if the arguments are out of bounds.</returns>
    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall)]
    public static extern long tjBufSize(int width, int height, int jpegSubsamp);

    /// <summary>
    /// Retrieve information about a JPEG image without decompressing it.
    /// </summary>
    /// <param name="_jpegDecompressor"> a handle to a TurboJPEG decompressor  or transformer instance </param>
    /// <param name="_compressedImage"> pointer to a buffer containing a JPEG image</param>
    /// <param name="_jpegSize">jpegSize size of the JPEG </param>
    /// <param name="width">width pointer to an integer variable that will receive the width(in pixels) of the JPEG image </param>
    /// <param name="height">height pointer to an integer variable that will receive the height(in pixels) of the JPEG image </param>
    /// <param name="jpegSubsamp">pointer to an integer variable that will receive the level of chrominance subsampling used when compressing the JPEG image</param>
    /// <returns>0 if successful, or -1 if an error</returns>
    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int tjDecompressHeader2(IntPtr _jpegDecompressor, byte[] _compressedImage, long _jpegSize, ref int width, ref int height, ref int jpegSubsamp);


    /// <summary>
    /// Decompress a JPEG image to an RGB or grayscale image.
    /// </summary>
    /// <param name="_jpegDecompressor">a handle to a TurboJPEG decompressor or transformer instance</param>
    /// <param name="_compressedImage">jpegBuf pointer to a buffer containing the JPEG image to decompress</param>
    /// <param name="_jpegSize">jpegSize size of the JPEG image (in bytes)</param>
    /// <param name="buffer">dstBuf pointer to an image buffer that will receive the decompressed image</param>
    /// <param name="width">desired width (in pixels) of the destination image. If this is different than the width of the JPEG image being decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest possible image that will fit within the desired width.If <tt>width</tt> is set to 0, then only the height will be considered when determining the scaled image size</param>
    /// <param name="pitch">bytes per line of the destination image</param>
    /// <param name="height">desired height (in pixels) of the destination image. If this is different than the height of the JPEG image being decompressed, then TurboJPEG will use scaling in the JPEG decompressor to generate the largest possible image that will fit within the desired height. If <tt>height</tt> is set to 0, then only the width will be considered when determining the scaled image size</param>
    /// <param name="pixelFormat">pixel format of the destination image</param>
    /// <param name="flags">flags the bitwise OR of one or more of the @ref TJFLAG_BOTTOMUP</param>
    /// <returns>0 if successful, or -1 if an error</returns>
    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int tjDecompress2(IntPtr _jpegDecompressor, byte[] _compressedImage, long _jpegSize, byte[] buffer, int width, int pitch, int height, int pixelFormat, int flags);

    [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern void tjDestroy(IntPtr _jpegHandler);

    // todo tjGetErrorStrtjGetErrorStr

    public class BufferAllocator : IDisposable
    {
        /// <summary>
        /// Free an image buffer previously allocated by TurboJPEG.
        /// 
        /// You should always use this function to free JPEG destination buffer(s) that were automatically
        /// (re)allocated by #tjCompress2() or #tjTransform() or that were manually allocated using #tjAlloc().
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall)]
        private static extern void tjFree(IntPtr buffer);

        /// <summary>
        /// Allocate an image buffer for use with TurboJPEG.
        /// 
        /// You should always use this function to allocate the JPEG destination buffer(s)
        /// for #tjCompress2() and #tjTransform() unless you are disabling automatic
        /// buffer (re)allocation (by setting #TJFLAG_NOREALLOC.)
        /// </summary>
        /// <param name="bytes">bytes the number of bytes to allocate</param>
        /// <returns>a pointer to a newly-allocated buffer with the specified number of bytes</returns>
        [DllImport(NATIVE_LIBRARY_NAME, CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr tjAlloc(int bytes);

        private IntPtr bufferHandle;
        private long bufferSize;
        private bool disposed = false;


        public BufferAllocator(int width, int height, int subsampling)
        {
            // gets max buffer size from the library
            bufferSize = LibJPEGTurbo.tjBufSize(width, height, subsampling);

            // allocates this buffer
            bufferHandle = tjAlloc((int) bufferSize);
        }

        #region IDisposable implementation - calls tjFree
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. ***Only unmanaged resources can be disposed***.
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    // none so far
                }

                // Call the appropriate methods to clean up
                // unmanaged resources here.
                // If disposing is false,
                // only the following code is executed.
                tjFree(bufferHandle);
                bufferHandle = IntPtr.Zero;

                // Note disposing has been done.
                disposed = true;
            }
        }

        // Use C# destructor (finalizer) syntax for finalization code.
        // This destructor will run only if the Dispose method
        // does not get called.
        // It gives your base class the opportunity to finalize.
        // Do not provide destructors in types derived from this class.
        ~BufferAllocator()
        {
            Dispose(false);
        }
        #endregion

    };


}
