using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace AprilTag.Interop {

public sealed class ImageU8 : SafeHandleZeroOrMinusOneIsInvalid
{
    #region SafeHandle implementation

    ImageU8() : base(true) {}

    protected override bool ReleaseHandle()
    {
        _Destroy(handle);
        return true;
    }

    #endregion

    #region image_u8 struct representation

    [StructLayout(LayoutKind.Sequential)]
    internal struct InternalData
    {
        internal int width;
        internal int height;
        internal int stride;
        internal IntPtr buf;
    }

    unsafe ref InternalData Data
      => ref Util.AsRef<InternalData>((void*)handle);

        #endregion

        #region Public properties and methods
        
        public int Width => Data.width;
        public int Height => Data.height;
        public int Stride => Data.stride;

        unsafe public byte* GetBufferPtr()
        {
            if (Data.buf == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError("[AprilTag] ImageU8 Data.buf is NULL inside GetBufferPtr!");
                return null;
            }

            // Check if the buffer contains writable memory
            byte* bufferPtr = (byte*)Data.buf;

            if (bufferPtr == null)
            {
                UnityEngine.Debug.LogError("[AprilTag] ImageU8 buffer pointer is NULL after casting!");
                return null;
            }

            UnityEngine.Debug.Log($"[AprilTag] Accessing ImageU8 buffer at {Data.buf}, size: {Stride * Height}");

            // 🚨 **Try Writing a Test Byte to Detect Invalid Memory**
            try
            {
                bufferPtr[0] = 0; // Write a dummy value
                UnityEngine.Debug.Log("[AprilTag] Memory write test succeeded!");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[AprilTag] Memory write test failed! {e.Message}");
                return null;
            }

            return bufferPtr;
        }

        public static ImageU8 Create(int width, int height)
        {
            //UnityEngine.Debug.Log($"[AprilTag] Requesting ImageU8 creation with stride={width} for {width}x{height}.");
            ImageU8 image = _CreateStride((uint)width, (uint)height, (uint)width);

            if (image == null || image.IsInvalid)
            {
                UnityEngine.Debug.LogError("[AprilTag] ImageU8.Create() failed! Native function returned NULL.");
                return null;
            }

            if (image.Data.buf == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError("[AprilTag] ImageU8 buffer pointer is NULL after allocation!");
                return null;
            }

            //UnityEngine.Debug.Log($"[AprilTag] ImageU8 created! Buffer ptr: {image.Data.buf}");
            return image;
        }

        #endregion

        #region Unmanaged interface

        [DllImport(Config.DllName, EntryPoint="image_u8_create_stride")]
        static extern ImageU8 _CreateStride(uint width, uint height, uint stride);

        [DllImport(Config.DllName, EntryPoint="image_u8_create")]
        static extern ImageU8 _Create(uint width, uint height);

        [DllImport(Config.DllName, EntryPoint="image_u8_destroy")]
        static extern void _Destroy(IntPtr image);

        #endregion
    }

} // namespace AprilTag.Interop
