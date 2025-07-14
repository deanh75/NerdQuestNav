using System;
using static AprilTag.AprilTagManager;

namespace ATE
{
    public static class ATE
    {
        public static double[] ToDoubleArray(this PoseData pose)
        {
            double[] darray = new double[]
            {
                pose.tx,
                pose.ty,
                pose.tz,
                pose.r1,
                pose.r2,
                pose.r3,
                pose.r4,
                pose.r5,
                pose.r6,
                pose.r7,
                pose.r8,
                pose.r9,
                pose.error,
            };

            return darray;
        }

        public static string toString(this PoseData pose)
        {
            return $"tx: {pose.tx}, ty: {pose.ty}, tz: {pose.tz}, r1: {pose.r1}, r2: {pose.r2}, r3: {pose.r3}, r4: {pose.r4}, r5: {pose.r5}, r6: {pose.r6}, r7: {pose.r7}, r8: {pose.r8}, r9: {pose.r9}, error: {pose.error}";
        }
    }
}
