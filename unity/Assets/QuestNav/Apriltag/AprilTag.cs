using System;
using Meta.XR;
using QuestNav.Camera;
using UnityEngine;
using UnityEngine.Rendering;
using static QuestNavAprilTag.AprilTagNative;
using TMPro;

namespace QuestNavAprilTag
{
    public class AprilTag
    {
        private int Ok = 0;
        private AprilTagPose[] Poses = new AprilTagPose[2];
        private readonly PassthroughFrameSource FrameSource;
        private readonly PassthroughCameraAccess CameraAccess;
        private readonly PassthroughCameraAccess.CameraIntrinsics Intrinsics;
        private readonly TMP_Text AtState;
        public AprilTag(PassthroughCameraAccess cameraAccess, PassthroughFrameSource frameSource, TMP_Text atState)
        {
            CameraAccess = cameraAccess;
            FrameSource = frameSource;
            Intrinsics = CameraAccess.Intrinsics;
            AtState = atState;

            Ok = apriltag_init(2.0f, 0.0f, 1, 1);
        }

        public void Periodic()
        {
            if (Ok == 1)
            {
                ProcessFrameAsync(FrameSource, (gray, width, height) =>
                {
                    if (gray == null) return;

                    int count = detect_apriltag_pose(
                        gray, width, height,
                        Intrinsics.FocalLength.x, Intrinsics.FocalLength.y, Intrinsics.PrincipalPoint.x, Intrinsics.PrincipalPoint.y,
                        0.1651f,
                        Poses, Poses.Length
                    );

                    String data = "";
                    for (int i = 0; i < count; i++)
                    {
                        data += Poses[i].ToString();
                    }
                    
                    AtState.text = "Tags: " + count + ", Data: " + data;
                });
            }
        }

        public void Shutdown()
        {
            if (Ok == 1)
            {
                apriltag_shutdown();
            }
        }

        private static void TextureToGrayscaleAsync(Texture2D texture, Action<byte[]> callback)
        {
            // Make sure texture format is RGBA32
            if (texture.format != TextureFormat.RGBA32)
            {
                Debug.LogWarning($"Texture format is {texture.format}, converting to RGBA32");
                Texture2D temp = new(texture.width, texture.height, TextureFormat.RGBA32, false);
                temp.SetPixels32(texture.GetPixels32());
                temp.Apply();
                texture = temp;
            }

            AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBA32, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback failed");
                    callback?.Invoke(null);
                    return;
                }

                var rawData = request.GetData<byte>();
                byte[] gray = new byte[texture.width * texture.height];

                // Convert RGBA to grayscale
                for (int i = 0; i < gray.Length; i++)
                {
                    int idx = i * 4;
                    byte r = rawData[idx];
                    byte g = rawData[idx + 1];
                    byte b = rawData[idx + 2];
                    gray[i] = (byte)(r * 0.299f + g * 0.587f + b * 0.114f);
                }

                callback?.Invoke(gray);
            });
        }

        private static void ProcessFrameAsync(PassthroughFrameSource frameSource, Action<byte[], int, int> callback)
        {
            if (frameSource?.CurrentFrame == null)
            {
                callback?.Invoke(null, 0, 0);
                return;
            }

            // Decode JPEG bytes to Texture2D
            Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(frameSource.CurrentFrame.frameData);

            // Convert to grayscale asynchronously
            TextureToGrayscaleAsync(tex, gray =>
            {
                if (gray != null)
                    callback?.Invoke(gray, tex.width, tex.height);
                else
                    callback?.Invoke(null, 0, 0);

                // Optional: destroy temporary texture to free memory
                UnityEngine.Object.Destroy(tex);
            });
        }
    }
}
