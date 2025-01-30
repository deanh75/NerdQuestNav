using AprilTag.Interop;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using System.Reflection;

namespace Anaglyph.DisplayCapture
{
	[DefaultExecutionOrder(-1000)]
    public class DisplayCaptureManager : MonoBehaviour
    {
        public static DisplayCaptureManager Instance { get; private set; }

		public bool startScreenCaptureOnStart = true;
		public bool flipTextureOnGPU = false;

        // April Tag Setup
        [SerializeField] float _tagSize = 0.16f;
        [SerializeField] Material _tagMaterial = null;
        [SerializeField] int _decimation = 1;
        AprilTag.TagDetector _detector;
        TagDrawer _drawer;
        private int kernelHandle;
        public ComputeShader grayscaleShader;
        private RenderTexture outputTexture;
        public ImageU8 image;

        [SerializeField] private Vector2Int textureSize = new(1600, 1600);
		public Vector2Int Size => textureSize;

		private Texture2D screenTexture;
		public Texture2D ScreenCaptureTexture => screenTexture;
		
		private RenderTexture flipTexture;

        public Matrix4x4 ProjectionMatrix { get; private set; }

		public UnityEvent<Texture2D> onTextureInitialized = new();
		public UnityEvent onStarted = new();
		public UnityEvent onPermissionDenied = new();
		public UnityEvent onStopped = new();
		public UnityEvent onNewFrame = new();

		private unsafe sbyte* imageData;
		private int bufferSize;

        

        private class AndroidInterface
		{
			private AndroidJavaClass androidClass;
			private AndroidJavaObject androidInstance;

			public AndroidInterface(GameObject messageReceiver, int textureWidth, int textureHeight)
			{
				androidClass = new AndroidJavaClass("com.trev3d.DisplayCapture.DisplayCaptureManager");
				androidInstance = androidClass.CallStatic<AndroidJavaObject>("getInstance");
				androidInstance.Call("setup", messageReceiver.name, textureWidth, textureHeight);
			}

			public void RequestCapture() => androidInstance.Call("requestCapture");
			public void StopCapture() => androidInstance.Call("stopCapture");

			public unsafe sbyte* GetByteBuffer()
			{
				AndroidJavaObject byteBuffer = androidInstance.Call<AndroidJavaObject>("getByteBuffer");
				return AndroidJNI.GetDirectBufferAddress(byteBuffer.GetRawObject());
			}
		}

		private AndroidInterface androidInterface;

		private void Awake()
		{
			Instance = this;

			androidInterface = new AndroidInterface(gameObject, Size.x, Size.y);

			screenTexture = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, 1, false);

			_detector = new AprilTag.TagDetector(Size.x, Size.y, _decimation);

            _drawer = new TagDrawer(_tagMaterial);
        }

		private void Start()
		{
            AprilTagLibraryLoader.LoadLibrary();

            flipTexture = new RenderTexture(Size.x, Size.y, 1, RenderTextureFormat.ARGB32, 1);
			flipTexture.Create();

            onTextureInitialized.Invoke(screenTexture);

			if (startScreenCaptureOnStart)
			{
				StartScreenCapture();
			}
			bufferSize = Size.x * Size.y * 4; // RGBA_8888 format: 4 bytes per pixel

            if (grayscaleShader == null)
            {
                Debug.LogError("Compute Shader is missing! Assign it in the Inspector.");
                return;
            }
            kernelHandle = grayscaleShader.FindKernel("CSMain");
        }

		public void StartScreenCapture()
		{
			androidInterface.RequestCapture();
		}

		public void StopScreenCapture()
		{
			androidInterface.StopCapture();
		}

		// Messages sent from Android

#pragma warning disable IDE0051 // Remove unused private members
		private unsafe void OnCaptureStarted()
		{
			onStarted.Invoke();
			imageData = androidInterface.GetByteBuffer();
		}

		private void OnPermissionDenied()
		{
			onPermissionDenied.Invoke();
		}


        private unsafe void OnNewFrameAvailable()
        {
            UnityEngine.Debug.Log("[AprilTag] 🔄 New frame received!");

            if (imageData == default)
            {
                UnityEngine.Debug.LogWarning("[AprilTag] ⚠️ Image data is default. Frame processing skipped.");
                return;
            }

            // 🚀 Step 1: Load the raw data into the screen texture
            UnityEngine.Debug.Log("[AprilTag] 🖼️ Updating screen texture...");
            screenTexture.LoadRawTextureData((IntPtr)imageData, bufferSize);
            screenTexture.Apply(); // Updates the GPU-side texture
            UnityEngine.Debug.Log("[AprilTag] ✅ Screen texture updated successfully!");

            // 🚀 Step 2: If GPU flip is enabled, process using RenderTexture
            if (flipTextureOnGPU)
            {
                UnityEngine.Debug.Log("[AprilTag] 🔄 Flipping texture on GPU...");
                Graphics.Blit(screenTexture, flipTexture, new Vector2(1, -1), Vector2.zero);
                Graphics.CopyTexture(flipTexture, screenTexture);
                UnityEngine.Debug.Log("[AprilTag] ✅ Texture flipped successfully!");
            }

            // 🚀 Step 3: Perform Async GPU Readback
            UnityEngine.Debug.Log("[AprilTag] 🎮 Requesting Async GPU Readback...");
            AsyncGPUReadback.Request(screenTexture, 0, request =>
            {
                if (request.hasError)
                {
                    UnityEngine.Debug.LogError("[AprilTag] ❌ Async GPU Readback failed!");
                    return;
                }
                UnityEngine.Debug.Log("[AprilTag] ✅ Async GPU Readback succeeded!");

                NativeArray<byte> grayscaleData = request.GetData<byte>();

                // 🚀 Step 4: Create ImageU8 instance
                UnityEngine.Debug.Log("[AprilTag] 🏗️ Creating ImageU8...");
                ImageU8 image = ImageU8.Create(screenTexture.width, screenTexture.height);
                if (image == null)
                {
                    UnityEngine.Debug.LogError("[AprilTag] ❌ ImageU8 creation failed!");
                    return;
                }
                UnityEngine.Debug.Log("[AprilTag] ✅ ImageU8 created successfully!");

                // 🚀 Step 5: Check ImageU8 buffer pointer
                // 🚀 Step 1: Access InternalData struct first
                object internalDataObj = typeof(ImageU8)
                    .GetProperty("Data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                    .GetValue(image);

                if (internalDataObj == null)
                {
                    UnityEngine.Debug.LogError("[AprilTag] ❌ Failed to access ImageU8 internal data.");
                    return;
                }

                // 🚀 Step 2: Now access the `buf` field inside InternalData
                FieldInfo bufField = internalDataObj.GetType().GetField("buf", BindingFlags.NonPublic | BindingFlags.Instance);
                if (bufField == null)
                {
                    UnityEngine.Debug.LogError("[AprilTag] ❌ Failed to access ImageU8 buffer field inside InternalData.");
                    return;
                }

                IntPtr bufferPtr = (IntPtr)bufField.GetValue(internalDataObj);

                if (bufferPtr == IntPtr.Zero)
                {
                    UnityEngine.Debug.LogError("[AprilTag] ❌ ImageU8 buffer pointer is STILL NULL after allocation!");
                    return;
                }
                else
                {
                    UnityEngine.Debug.Log("[AprilTag] ✅ ImageU8 buffer pointer is VALID: " + bufferPtr);
                }

                UnityEngine.Debug.Log($"[AprilTag] 🛠️ ImageU8 Buffer Info - Width: {image.Width}, Height: {image.Height}, Stride: {image.Stride}");
                UnityEngine.Debug.Log($"[AprilTag] 🛠️ ImageU8 Data.buf: {bufferPtr}");

                // ✅ Check Buffer Size
                int bufferSize = image.Stride * image.Height;
                if (bufferSize <= 0)
                {
                    UnityEngine.Debug.LogError($"[AprilTag] ❌ Invalid buffer size: {bufferSize}. Cannot copy.");
                    return;
                }

                UnityEngine.Debug.Log($"[AprilTag] ✅ Buffer Size: {bufferSize}");

                unsafe
                {
                    byte* destPtr = image.GetBufferPtr(); // ✅ Get the raw pointer directly

                    if (destPtr == null)
                    {
                        UnityEngine.Debug.LogError("[AprilTag] ❌ ImageU8 buffer pointer is null or inaccessible. Cannot copy.");
                        return;
                    }

                    UnityEngine.Debug.Log("[AprilTag] 📋 Copying grayscale data to ImageU8...");
                    try
                    {
                        UnsafeUtility.MemCpy(destPtr, grayscaleData.GetUnsafeReadOnlyPtr(), grayscaleData.Length);
                        UnityEngine.Debug.Log("[AprilTag] ✅ Grayscale data copied successfully!");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"[AprilTag] ❌ MemCpy failed! {e.Message}");
                        return;
                    }
                }

                // 🚀 Step 7: Perform AprilTag detection
                UnityEngine.Debug.Log("[AprilTag] 🔍 Running AprilTag detection...");
                var fov = 82.0f * Mathf.Deg2Rad;
                _detector.ProcessImage(image, fov, _tagSize);
                UnityEngine.Debug.Log("[AprilTag] ✅ AprilTag detection complete!");

                // 🚀 Step 8: Draw detected tags
                foreach (var tag in _detector.DetectedTags)
                {
                    _drawer.Draw(7, tag.Position, tag.Rotation, _tagSize);
                }
                UnityEngine.Debug.Log("[AprilTag] 🖼️ Tags drawn successfully!");

                // 🚀 Step 9: Fire new frame event
                onNewFrame.Invoke();
                UnityEngine.Debug.Log("[AprilTag] ✅ New frame event invoked!");
            });
        }

        /*private unsafe void OnNewFrameAvailable()
        {
            if (imageData == default)
            {
                UnityEngine.Debug.LogWarning("Image data is default. Frame processing skipped.");
                return;
            }

            // Load the raw data into the screen texture
            screenTexture.LoadRawTextureData((IntPtr)imageData, bufferSize);
            screenTexture.Apply(); // Updates the GPU-side texture
            //UnityEngine.Debug.Log("Screen texture updated with new frame data.");

            // Convert the screen texture to ImageU8
            ImageU8 image = ConvertTexture2DToImageU8(screenTexture);
            if (image == null)
            {
                UnityEngine.Debug.LogError("[AprilTag]Failed to convert screen texture to ImageU8. Frame processing skipped.");
                return;
            }

            // If flipTextureOnGPU is enabled, process using RenderTexture
            if (flipTextureOnGPU)
            {
                Graphics.Blit(screenTexture, flipTexture, new Vector2(1, -1), Vector2.zero);
                Graphics.CopyTexture(flipTexture, screenTexture);
                //UnityEngine.Debug.Log("Screen texture flipped on GPU.");
            }

            // Perform CPU-side processing (e.g., AprilTag detection)
            var fov = 82.0f * Mathf.Deg2Rad;
            _detector.ProcessImage(image, fov, _tagSize);

            //Debug.Log("[AprilTag]Detected " + _detector.DetectedTags.Count() + " tags");

            // Draw tag visualization
            foreach (var tag in _detector.DetectedTags)
            {
                _drawer.Draw(7, tag.Position, tag.Rotation, _tagSize);
            }

            // Invoke the event for a new frame
            onNewFrame.Invoke();
            //UnityEngine.Debug.Log("[AprilTag]New frame processing completed.");
        }

        public ImageU8 ConvertTexture2DToImageU8(Texture2D inputTexture)
        {
            if (inputTexture == null)
                throw new ArgumentNullException(nameof(inputTexture));

            int width = inputTexture.width;
            int height = inputTexture.height;

            // Create a single-channel (R8) RenderTexture for output
            outputTexture = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            outputTexture.enableRandomWrite = true;
            outputTexture.Create();

            // Assign textures to the compute shader
            grayscaleShader.SetTexture(kernelHandle, "_InputTexture", inputTexture);
            grayscaleShader.SetTexture(kernelHandle, "_OutputTexture", outputTexture);

            // Dispatch compute shader
            int threadGroupsX = Mathf.CeilToInt(width / 8f);
            int threadGroupsY = Mathf.CeilToInt(height / 8f);
            grayscaleShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

            // Read processed data into a Texture2D
            Texture2D resultTexture = new Texture2D(width, height, TextureFormat.R8, false);
            RenderTexture.active = outputTexture;
            resultTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resultTexture.Apply();
            RenderTexture.active = null;

            // Get grayscale data from the processed texture
            NativeArray<byte> grayscaleData = resultTexture.GetRawTextureData<byte>();

            // Create ImageU8
            ImageU8 image = ImageU8.Create(width, height);

            // Copy grayscale data to ImageU8 buffer (zero-copy approach)
            unsafe
            {
                byte* destPtr = (byte*)image.Buffer[0];
                UnsafeUtility.MemCpy(destPtr, grayscaleData.GetUnsafeReadOnlyPtr(), grayscaleData.Length);
            }

            // Cleanup
            UnityEngine.Object.Destroy(resultTexture);
            UnityEngine.Object.Destroy(outputTexture);

            return image;
        }*/

        private void OnCaptureStopped()
        {
            onStopped.Invoke();
        }
        
#pragma warning restore IDE0051 // Remove unused private members
    }
}