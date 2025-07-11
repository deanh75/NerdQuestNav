using static AprilTag.AprilTagManager;

namespace ATE
{
    public static class ATE
    {
        public static double[] ToDoubleArray(this PoseData pose)
        {
            return new double[] { pose.tx, pose.ty, pose.tz, pose.r1, pose.r2, pose.r3, pose.r4, pose.r5, pose.r6, pose.r7, pose.r8, pose.r9, pose.error };
        }
    }
}
