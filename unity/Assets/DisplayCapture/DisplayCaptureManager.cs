using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

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

        [SerializeField] private Vector2Int textureSize = new(1024, 1024);
		public Vector2Int Size => textureSize;

		private Texture2D screenTexture;
		private Texture2D aprilTagTexture;
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

            aprilTagTexture = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, 1, false);

			_detector = new AprilTag.TagDetector(1024,1024, _decimation);

            _drawer = new TagDrawer(_tagMaterial);
        }

		private void Start()
		{
			flipTexture = new RenderTexture(Size.x, Size.y, 1, RenderTextureFormat.ARGB32, 1);
			flipTexture.Create();

            onTextureInitialized.Invoke(screenTexture);

			if (startScreenCaptureOnStart)
			{
				StartScreenCapture();
			}
			bufferSize = Size.x * Size.y * 4; // RGBA_8888 format: 4 bytes per pixel
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
            if (imageData == default) return;

            // Load the raw data into the screen texture
            screenTexture.LoadRawTextureData((IntPtr)imageData, bufferSize);
            screenTexture.Apply(); // Updates the GPU-side texture

            // If flipTextureOnGPU is enabled, process using RenderTexture
            if (flipTextureOnGPU)
            {
                Graphics.Blit(screenTexture, flipTexture, new Vector2(1, -1), Vector2.zero);
                Graphics.CopyTexture(flipTexture, screenTexture);
            }

            // Access the pixel data as a ReadOnlySpan<Color32> (optional)
            ReadOnlySpan<Color32> pixelData = screenTexture.GetRawTextureData<Color32>();

            // Perform CPU-side processing (e.g., AprilTag detection)
            var fov = 82.0f * Mathf.Deg2Rad;
            _detector.ProcessImage(pixelData, fov, _tagSize);

			Debug.Log("[AprilTag] Detected " + _detector.DetectedTags.Count() + " tags");

            // Visualize detected tags
            foreach (var tag in _detector.DetectedTags)
            {
                _drawer.Draw(99, tag.Position, tag.Rotation, _tagSize);
            }

            // Invoke the event for a new frame
            onNewFrame.Invoke();

        }

		private void OnCaptureStopped()
		{
            onStopped.Invoke();
		}
#pragma warning restore IDE0051 // Remove unused private members
	}
}