using PassthroughCameraSamples;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QuestNav.AprilTag;
using static TagDrawerExt.TagDrawerExt;

namespace AprilTag
{
    public class AprilTagManager : MonoBehaviour
    {
        // Create a field to attach the reference to the WebCamTextureManager prefab
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        [SerializeField] private TMP_Text m_debugText;
        [SerializeField] Material tagMaterial;
        private readonly RawImage m_image;
        private string m_pnmpath;
        private float m_fx;
        private float m_fy;
        private float m_cx;
        private float m_cy;
        private TagDrawer m_drawer;

        private IEnumerator Start()
        {
            while (m_webCamTextureManager == null ||  m_webCamTextureManager.WebCamTexture == null)
            {
                yield return null;
            }
            m_debugText.text += "\nWebCamTexture Object ready and playing.";
            // Set WebCamTexture GPU texture to the RawImage Ui element
            m_image.texture = m_webCamTextureManager.WebCamTexture;

            PassthroughCameraIntrinsics intrinsics = new();

            m_fx = intrinsics.FocalLength.x;
            m_fy = intrinsics.FocalLength.y;
            m_cx = intrinsics.PrincipalPoint.x;
            m_cy = intrinsics.PrincipalPoint.y;

            m_drawer = new(tagMaterial);
        }

        public PoseData AprilTagPose()
        {
            m_debugText.text = PassthroughCameraPermissions.HasCameraPermission == true ? "Permission granted." : "No permission granted.";

            m_pnmpath = SaveTextureAsPNM(m_image);

            PoseData pose = detect_and_estimate_pose(m_pnmpath, m_fx, m_fy, m_cx, m_cy);

            m_drawer.Draw(pose.ToVector(), pose.ToQuaternion(), 0.05f);
            return pose;
        }

        private string SaveTextureAsPNM(RawImage rawImage)
        {
            // Get Texture2D from RawImage
            Texture2D texture = rawImage.texture as Texture2D;
            if (texture == null)
            {
                m_debugText.text = "RawImage does not have a valid Texture2D!";
                return null;
            }

            // Get pixel data
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            // P6 PNM header (Binary RGB format)
            StringBuilder header = new();
            header.AppendLine("P6");  // Magic number for binary RGB PNM
            header.AppendLine($"{width} {height}"); // Image dimensions
            header.AppendLine("255"); // Max color value

            // Convert pixel data to raw binary RGB
            byte[] pixelBytes = new byte[width * height * 3];  // RGB format
            for (int i = 0; i < pixels.Length; i++)
            {
                pixelBytes[i * 3] = pixels[i].r;
                pixelBytes[i * 3 + 1] = pixels[i].g;
                pixelBytes[i * 3 + 2] = pixels[i].b;
            }

            // Save to file
            string path = Path.Combine(Application.persistentDataPath, "image.pnm");
            using (FileStream fileStream = new(path, FileMode.Create))
            {
                byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
                fileStream.Write(headerBytes, 0, headerBytes.Length);
                fileStream.Write(pixelBytes, 0, pixelBytes.Length);
            }

            m_debugText.text = "PNM image saved at: " + path;

            return path;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PoseData
        {
            public double tx, ty, tz;
            public double r1, r2, r3, r4, r5, r6, r7, r8, r9;
        };

        [DllImport("apriltag_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern PoseData detect_and_estimate_pose(string image_path, double fx, double fy, double cx, double cy);
    }
}
