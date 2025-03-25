using System;
using System.Collections;
using AprilTag;
using AprilTag.Interop;
using QuestNav.AprilTag;
using QuestNav.Passthrough;
using QuestNav.Utils;
using UnityEngine;
using Unity.Collections;
using Pose = UnityEngine.Pose;

namespace QuestNav.Fiducial
{
    /// <summary>
    /// Integrates AprilTag detection with Quest passthrough cameras.
    /// Handles the transformation from 2D camera detections to 3D world space.
    /// </summary>
    public class FiducialDetector : MonoBehaviour
    {
        [SerializeField] WebCamTextureManager cameraManager;
        [SerializeField] int decimation = 4;
        [SerializeField] float tagSize = 0.05f; // Physical size in meters
        [SerializeField] Material tagMaterial;
        [SerializeField] bool debugVisualization = true;
    
        private TagDetector detector;
        private TagDrawer drawer;
        private bool isInitialized;
        private Color32[] pixelBuffer;
        private NativeArray<Color32> pixelData;
    
        // Updated event for tag detection using TagPose struct
        public delegate void TagDetectedHandler(TagPose tagPose);
        public event TagDetectedHandler OnTagDetected;
    
        void Start()
        {
            drawer = new TagDrawer(tagMaterial);
            StartCoroutine(InitializeDetector());
        }
    
        private IEnumerator InitializeDetector()
        {
            // Wait for camera to be fully initialized
            while (cameraManager == null || cameraManager.WebCamTexture == null || 
                   cameraManager.WebCamTexture.width <= 0)
            {
                Debug.Log($"Fiducial detector init skipped. Camera not ready!");
                yield return null;
            }
        
            int width = cameraManager.WebCamTexture.width;
            int height = cameraManager.WebCamTexture.height;
            
            // Initialize pixel buffer
            pixelBuffer = new Color32[width * height];
        
            detector = new TagDetector(width, height, decimation);
            isInitialized = true;
        
            Debug.Log($"Fiducial detector initialized with camera resolution {width}x{height}");
        }
    
        void OnDestroy()
        {
            detector?.Dispose();
            drawer?.Dispose();
            
            // Clean up NativeArray if it exists
            if (pixelData.IsCreated)
                pixelData.Dispose();
        }
    
        void LateUpdate()
        {
            if (!isInitialized || cameraManager.WebCamTexture == null) 
                return;
        
            // Get image from camera
            var image = GetTextureSpan(cameraManager.WebCamTexture);
            if (image.IsEmpty) 
                return;
        
            // Get camera intrinsics for proper 3D positioning
            var intrinsics = PassthroughCameraUtils.GetCameraIntrinsics(cameraManager.Eye);
        
            // Calculate field of view from focal length and resolution
            float fovY = 2 * Mathf.Atan2(intrinsics.Resolution.y/2f, intrinsics.FocalLength.y);
        
            // Process image to detect tags
            detector.ProcessImage(image, fovY, tagSize);
        
            // Process detected tags
            foreach (var tag in detector.DetectedTags)
            {
                QueuedLogger.Log("Detected tag ID: " + tag.Detection.ID);
                // Calculate TagPose directly from detection data
                TagPose tagPose = CalculateTagPose(tag.Detection, intrinsics);
            
                // Draw the tag if debug visualization is enabled
                if (debugVisualization)
                {
                    drawer.Draw(tagPose.Detection.ID, tagPose.Position, tagPose.Rotation, tagSize);
                }
            
                // Trigger event with TagPose
                OnTagDetected?.Invoke(tagPose);
            }
            
            // Clean up NativeArray after use
            if (pixelData.IsCreated)
                pixelData.Dispose();
        }

        /// <summary>
        /// Efficiently converts WebCamTexture to ReadOnlySpan for processing without unsafe code
        /// </summary>
        private ReadOnlySpan<Color32> GetTextureSpan(WebCamTexture texture)
        {
            // Get the pixels from the WebCamTexture
            pixelBuffer = texture.GetPixels32(pixelBuffer);
            
            // Dispose old NativeArray if it exists
            if (pixelData.IsCreated)
                pixelData.Dispose();
                
            // Create new NativeArray from the pixel buffer
            pixelData = new NativeArray<Color32>(pixelBuffer, Allocator.TempJob);
            
            // Create span from NativeArray
            return pixelData.AsSpan();
        }
    
        /// <summary>
/// Calculates the 3D world pose of a detected tag using a more robust approach
/// </summary>
private TagPose CalculateTagPose(Detection detection, PassthroughCameraIntrinsics intrinsics)
{
    // Extract corner points from detection
    Vector2 center = new Vector2((float)detection.Center.x, (float)detection.Center.y);
    Vector2[] corners = new Vector2[4]
    {
        new Vector2((float)detection.Corner1.x, (float)detection.Corner1.y),
        new Vector2((float)detection.Corner2.x, (float)detection.Corner2.y),
        new Vector2((float)detection.Corner3.x, (float)detection.Corner3.y),
        new Vector2((float)detection.Corner4.x, (float)detection.Corner4.y)
    };

    // Define 3D model points (corners of tag in local space)
    Vector3[] modelPoints = new Vector3[4]
    {
        new Vector3(-tagSize/2, -tagSize/2, 0),
        new Vector3(tagSize/2, -tagSize/2, 0),
        new Vector3(tagSize/2, tagSize/2, 0),
        new Vector3(-tagSize/2, tagSize/2, 0)
    };

    // Solve for pose using PnP-like approach
    Matrix4x4 tagToCamera = SolvePoseFromCorners(corners, modelPoints, intrinsics);
    
    // Extract position and rotation from matrix
    Vector3 positionInCamera = tagToCamera.GetColumn(3);
    Quaternion rotationInCamera = QuaternionFromMatrix(tagToCamera);

    // Get camera pose in world space
    var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(cameraManager.Eye);

    // Transform to world space
    Vector3 worldPosition = cameraPose.position + cameraPose.rotation * positionInCamera;
    Quaternion worldRotation = cameraPose.rotation * rotationInCamera;

    return new TagPose(detection, worldPosition, worldRotation);
}

/// <summary>
/// Solves for 3D pose from 2D corner points using a PnP-like approach
/// </summary>
private Matrix4x4 SolvePoseFromCorners(Vector2[] imagePoints, Vector3[] modelPoints, 
    PassthroughCameraIntrinsics intrinsics)
{
    // Calculate average depth using multiple corners for more stability
    float avgDepth = 0;
    float totalWeight = 0;
    for (int i = 0; i < 4; i++)
    {
        int j = (i + 1) % 4;
        // Length of this edge in pixels
        float edgePixelLength = Vector2.Distance(imagePoints[i], imagePoints[j]);
        // Weight inversely by pixel length (more pixels = more reliable measurement)
        float weight = edgePixelLength;
        // Corresponding model edge length in meters
        float modelEdgeLength = Vector3.Distance(modelPoints[i], modelPoints[j]);
        // Calculate depth for this edge using perspective projection formula
        float depth = modelEdgeLength * intrinsics.FocalLength.x / edgePixelLength;
        
        avgDepth += depth * weight;
        totalWeight += weight;
    }
    avgDepth /= totalWeight;

    // Create rays for each corner
    Vector3[] cornerRays = new Vector3[4];
    for (int i = 0; i < 4; i++)
    {
        // Normalized direction vector from camera center through image point
        cornerRays[i] = new Vector3(
            (imagePoints[i].x - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
            (imagePoints[i].y - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
            1.0f
        ).normalized;
    }

    // Calculate tag orientation using corner rays
    Vector3 xAxis = (cornerRays[1] - cornerRays[0] + cornerRays[2] - cornerRays[3]).normalized;
    Vector3 yAxis = (cornerRays[3] - cornerRays[0] + cornerRays[2] - cornerRays[1]).normalized;
    
    // Ensure orthogonality
    Vector3 zAxis = Vector3.Cross(xAxis, yAxis).normalized;
    yAxis = Vector3.Cross(zAxis, xAxis).normalized;

    // Build transformation matrix
    Matrix4x4 result = Matrix4x4.identity;
    result.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
    result.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
    result.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
    
    // Calculate center ray and position
    Vector3 centerRay = new Vector3(
        ((imagePoints[0].x + imagePoints[1].x + imagePoints[2].x + imagePoints[3].x) / 4 - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
        ((imagePoints[0].y + imagePoints[1].y + imagePoints[2].y + imagePoints[3].y) / 4 - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
        1.0f
    ).normalized;
    
    // Set position
    result.SetColumn(3, new Vector4(centerRay.x * avgDepth, centerRay.y * avgDepth, centerRay.z * avgDepth, 1));
    
    return result;
}

/// <summary>
/// Improved method to extract quaternion from matrix that preserves tag orientation
/// </summary>
private Quaternion QuaternionFromMatrix(Matrix4x4 m)
{
    // Extract rotation columns
    Vector3 forward = new Vector3(m.m02, m.m12, m.m22).normalized;
    Vector3 up = new Vector3(m.m01, m.m11, m.m21).normalized;
    Vector3 right = new Vector3(m.m00, m.m10, m.m20).normalized;
    
    // This is critical - we need to flip the forward vector for correct tag orientation
    // AprilTags have +Z pointing away from the visible face
    forward = -forward;
    
    // Re-orthogonalize to ensure perfect orthogonal axes
    forward = forward.normalized;
    right = Vector3.Cross(up, forward).normalized;
    up = Vector3.Cross(forward, right).normalized;
    
    // Create matrix from corrected axes
    Matrix4x4 correctedMatrix = Matrix4x4.identity;
    correctedMatrix.SetColumn(0, new Vector4(right.x, right.y, right.z, 0));
    correctedMatrix.SetColumn(1, new Vector4(up.x, up.y, up.z, 0));
    correctedMatrix.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0));
    
    // Convert to quaternion using Unity's built-in method
    return Quaternion.LookRotation(forward, up);
}
    }
}