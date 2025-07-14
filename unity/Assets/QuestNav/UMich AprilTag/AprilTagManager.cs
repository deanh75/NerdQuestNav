using System;
using PassthroughCameraSamples;
using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using QuestNav.AprilTag;
using static TagDrawerExt.TagDrawerExt;
using ATE;

namespace AprilTag
{
    public class AprilTagManager : MonoBehaviour
    {
        // Create a field to attach the reference to the WebCamTextureManager prefab
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        [SerializeField] private TMP_Text m_debugText;
        [SerializeField] Material tagMaterial;
        private float m_fx;
        private float m_fy;
        private float m_cx;
        private float m_cy;
        private TagDrawer m_drawer;
        private PoseData pose;

        private IEnumerator Start()
        {
            while (m_webCamTextureManager == null ||  m_webCamTextureManager.WebCamTexture == null)
            {
                yield return null;
            }
            m_debugText.text = "\nWebCamTexture Object ready and playing.";

            PassthroughCameraIntrinsics intrinsics = new();
            m_fx = intrinsics.FocalLength.x;
            m_fy = intrinsics.FocalLength.y;
            m_cx = intrinsics.PrincipalPoint.x;
            m_cy = intrinsics.PrincipalPoint.y;

            m_drawer = new(tagMaterial);
        }

        private void OnDestroy()
        {
            destroy_detector();
        }

        public PoseData AprilTagPose()
        {
            GCHandle handle = GCHandle.Alloc(m_webCamTextureManager.WebCamTexture.GetPixels32(), GCHandleType.Pinned);

            try {
                IntPtr ptr = handle.AddrOfPinnedObject();  // Get pointer to Color32[]
                pose = detect_and_estimate_pose(ptr, m_webCamTextureManager.WebCamTexture.width, m_webCamTextureManager.WebCamTexture.height, 
                    m_fx, m_fy, m_cx, m_cy);
            } finally {
                handle.Free();  // Always unpin
            }

            m_debugText.text = pose.toString();

            m_drawer.Draw(pose.ToVector(), pose.ToQuaternion(), 0.05f);
            return pose;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PoseData
        {
            public double tx, ty, tz; // Translation in meters
            public double r1, r2, r3, r4, r5, r6, r7, r8, r9; // Rotation matrix as 3x3 flattened
            public double error; // Error in pose estimation
        };

        [DllImport("apriltag_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern PoseData detect_and_estimate_pose(IntPtr rgba_data, int width, int height, double fx, double fy, double cx, double cy);

        [DllImport("apriltag_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void destroy_detector();
    }
}
