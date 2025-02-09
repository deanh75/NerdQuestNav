using AprilTag.Interop;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Anaglyph.DisplayCapture
{
    [DefaultExecutionOrder(-1000)]
    public class DisplayCaptureManager : MonoBehaviour
    {
        public static DisplayCaptureManager Instance { get; private set; }

        [Header("Screen Capture Controls")]
        public bool startScreenCaptureOnStart = true;
        public bool flipTextureOnGPU = false;

        [Header("April Tag Setup")]
        [SerializeField] private float _tagSize = 0.16f;
        [SerializeField] private Material _tagMaterial = null;
        [SerializeField] private int _decimation = 1;

        [Header("April Tag Debugging")]
        [SerializeField] private Renderer quadRenderer;
        [SerializeField] private Material debugGrayscaleMat;

        [Header("Grayscale Conversion")]
        // Assign a small shader or material that outputs a single-channel R texture
        // For example, a simple pass that does:
        //   fixed4 frag (v2f i) : SV_Target {
        //       float4 c = tex2D(_MainTex, i.uv);
        //       float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));
        //       return float4(gray, gray, gray, 1.0);
        //   }
        [SerializeField] private Material grayscaleMaterial;

        // --- Private ---
        private AprilTag.TagDetector _detector;
        private TagDrawer _drawer;

        private Texture2D screenTexture;
        private RenderTexture flipTexture;     // If you still want to flip the RGBA
        private RenderTexture grayscaleRT;     // Single-channel (R8) output

        private AndroidInterface androidInterface;
        private int bufferSize;

        // We'll create a single shared ImageU8 for all frames:
        private ImageU8 sharedImage;
        private unsafe byte* sharedBufferPtr;

        [Header("Texture Size")]
        [SerializeField] private Vector2Int textureSize = new(1600, 1600);
        public Vector2Int Size => textureSize;

        [Header("Events")]
        public UnityEvent<Texture2D> onTextureInitialized = new();
        public UnityEvent onStarted = new();
        public UnityEvent onPermissionDenied = new();
        public UnityEvent onStopped = new();
        public UnityEvent onNewFrame = new();

        // --------------------------------------------------
        // Nested Android interface class for requesting capture
        // --------------------------------------------------
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

        private unsafe sbyte* imageData; // pointer returned from Android

        // --------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------
        private void Awake()
        {
            // Make sure the native library is loaded
            AprilTagLibraryLoader.LoadLibrary();

            // Singleton instance
            Instance = this;

            // Setup the Android interface
            androidInterface = new AndroidInterface(gameObject, Size.x, Size.y);

            // Create the screenTexture: 4 bytes/pixel (RGBA32)
            screenTexture = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, /*mipChain*/false);

            // Create the AprilTag detector
            _detector = new AprilTag.TagDetector(Size.x, Size.y, _decimation);

            // Create the tag visualization drawer
            _drawer = new TagDrawer(_tagMaterial);
        }

        private void Start()
        {
            // We create a small RT to do optional flipping
            flipTexture = new RenderTexture(Size.x, Size.y, /*depth*/0, RenderTextureFormat.ARGB32);
            flipTexture.Create();

            // Create a single-channel RenderTexture for grayscale
            // The GPU readback from this R8 texture will be exactly 1 byte/pixel
            grayscaleRT = new RenderTexture(Size.x, Size.y, 0, RenderTextureFormat.R8);
            grayscaleRT.enableRandomWrite = false;  // not strictly needed if just Blit
            grayscaleRT.Create();

            onTextureInitialized.Invoke(screenTexture);

            // Start capture if flagged
            if (startScreenCaptureOnStart)
            {
                StartScreenCapture();
            }

            // The raw RGBA buffer from Android
            bufferSize = Size.x * Size.y * 4;

            // Allocate a single `ImageU8` for all frames (1600x1600)
            sharedImage = ImageU8.Create(Size.x, Size.y);
            sharedImage = ImageU8.Create(Size.x, Size.y);

            if (sharedImage == null)
            {
                Debug.LogError("[AprilTag]Failed to allocate shared ImageU8!");
            }
            else
            {
                // Use an unsafe block to get and store the pointer
                unsafe
                {
                    byte* ptr = sharedImage.GetBufferPtr();
                    if (ptr == null)
                    {
                        Debug.LogError("[AprilTag] Shared ImageU8 buffer pointer is null!");
                    }
                    else
                    {
                        sharedBufferPtr = ptr;
                        Debug.Log($"[AprilTag] Shared ImageU8 allocated: stride={sharedImage.Stride}, total bytes={sharedImage.Stride * sharedImage.Height}");
                    }
                }
            }

            // Assume you assigned the Quad's MeshRenderer via Inspector
            // and also assigned DebugGrayscaleMat.
            // Then at runtime, set the material to that debug material.
            quadRenderer.material = debugGrayscaleMat;

            // Assign the R8 RenderTexture as _MainTex
            quadRenderer.material.SetTexture("_MainTex", grayscaleRT);
        }

        public void StartScreenCapture() => androidInterface?.RequestCapture();
        public void StopScreenCapture() => androidInterface?.StopCapture();

        // --------------------------------------------------
        // Android callback events
        // --------------------------------------------------
        private unsafe void OnCaptureStarted()
        {
            onStarted.Invoke();
            // This pointer points to the RGBA buffer on the Java side
            imageData = androidInterface.GetByteBuffer();
        }

        private void OnPermissionDenied()
        {
            onPermissionDenied.Invoke();
        }

        private void OnCaptureStopped()
        {
            onStopped.Invoke();
        }

        // --------------------------------------------------
        // *Most Important* OnNewFrameAvailable
        // --------------------------------------------------
        private unsafe void OnNewFrameAvailable()
        {
            if (imageData == default)
            {
                Debug.LogWarning("[AprilTag] imageData is default. No frame to process.");
                return;
            }

            // 1) Copy raw RGBA bytes from Android into screenTexture on CPU
            screenTexture.LoadRawTextureData((IntPtr)imageData, bufferSize);
            screenTexture.Apply(); // update GPU side

            // 2) Optionally flip on the GPU
            if (flipTextureOnGPU)
            {
                Graphics.Blit(screenTexture, flipTexture, new Vector2(1, -1), Vector2.zero);
                Graphics.CopyTexture(flipTexture, screenTexture);
            }

            // 3) Convert RGBA -> single-channel using a Blit to `grayscaleRT`.
            //    We assume `grayscaleMaterial` does the grayscale or the pass is just reading red channel, etc.
            if (grayscaleMaterial != null)
            {
                Graphics.Blit(screenTexture, grayscaleRT, grayscaleMaterial);
            }
            else
            {
                // Fallback: just copy red channel or something
                // This won't truly produce a single channel but let's assume it does for demonstration
                Graphics.Blit(screenTexture, grayscaleRT);
            }

            // 4) Async GPU readback from the single-channel texture
            AsyncGPUReadback.Request(grayscaleRT, 0, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("[AprilTag] Async GPU Readback failed!");
                    return;
                }

                // This data should be 1 byte/pixel (plus row alignment).
                NativeArray<byte> data = request.GetData<byte>();

                // Safety check
                int bytesFromGPU = data.Length;
                int expectedBytes = sharedImage.Stride * sharedImage.Height;
                if (bytesFromGPU < expectedBytes)
                {
                    Debug.LogError($"[AprilTag] GPU readback gave {bytesFromGPU} bytes, but ImageU8 expects {expectedBytes}.");
                    return;
                }

                // 5) Copy bytes from GPU readback into the shared ImageU8 buffer
                unsafe
                {
                    try
                    {
                        UnsafeUtility.MemCpy(sharedBufferPtr, data.GetUnsafeReadOnlyPtr(), expectedBytes);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[AprilTag] MemCpy failed: {e.Message}");
                        return;
                    }
                }

                // 6) Run AprilTag detection on the single-channel image
                float fov = 82.0f * Mathf.Deg2Rad;
                _detector.ProcessImage(sharedImage, fov, _tagSize);

                // 7) Draw the detected tags
                foreach (var tag in _detector.DetectedTags)
                {
                    _drawer.Draw(7, tag.Position, tag.Rotation, _tagSize);
                }

                // 8) Fire the new frame event
                onNewFrame.Invoke();
            });
        }
    }
}
