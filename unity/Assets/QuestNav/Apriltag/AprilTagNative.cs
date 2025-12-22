using System.Runtime.InteropServices;

namespace QuestNavAprilTag
{
    public static class AprilTagNative
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct AprilTagPose
        {
            public int id;
            public float tx, ty, tz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
            public float[] R;

            public override string ToString()
            {
                return $"{{ id={id}, " +
                       $"t=({tx:F3}, {ty:F3}, {tz:F3}), " +
                       $"R=[{R[0]:F2}, {R[1]:F2}, {R[2]:F2}; " +
                       $"{R[3]:F2}, {R[4]:F2}, {R[5]:F2}; " +
                       $"{R[6]:F2}, {R[7]:F2}, {R[8]:F2}] }}";
            }
        }

        [DllImport("apriltag_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int apriltag_init(
            float quadDecimate,
            float quadSigma,
            int nthreads,
            int refineEdges
        );

        [DllImport("apriltag_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern void apriltag_shutdown();

        [DllImport("apriltag_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int detect_apriltag_pose(
            byte[] gray,
            int width,
            int height,
            float fx,
            float fy,
            float cx,
            float cy,
            float tagSize,
            [In, Out] AprilTagPose[] out_poses,  // array of AprilTagPose
            int maxPoses
        );
    }

}
