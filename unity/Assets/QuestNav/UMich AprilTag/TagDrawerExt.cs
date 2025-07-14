using System;
using UnityEngine;
using static AprilTag.AprilTagManager;

namespace TagDrawerExt
{
    public static class TagDrawerExt
    {
        public static Vector3 ToVector(this PoseData pose)
        {
            return new Vector3
            {
                x = (float)pose.tx,
                y = (float)pose.ty,
                z = (float)pose.tz,
            };
        }

        public static Quaternion ToQuaternion(this PoseData pose)
        {
            double trace = pose.r1 + pose.r5 + pose.r9; // [0][0] + [1][1] + [2][2]
            double w,
                x,
                y,
                z;

            if (trace > 0)
            {
                double s = 0.5 / Math.Sqrt(trace + 1.0);
                w = 0.25 / s;
                x = (pose.r8 - pose.r6) * s; // [2][1] - [1][2]
                y = (pose.r3 - pose.r7) * s; // [0][2] - [2][0]
                z = (pose.r4 - pose.r2) * s; // [1][0] - [0][1]
            }
            else
            {
                if (pose.r1 > pose.r5 && pose.r1 > pose.r9) // [0][0] > [1][1] && [0][0] > [2][2]
                {
                    double s = 2.0 * Math.Sqrt(1.0 + pose.r1 - pose.r5 - pose.r9); // 1.0 + [0][0] - [1][1] - [2][2]
                    w = (pose.r8 - pose.r6) / s; // [2][1] - [1][2]
                    x = 0.25 * s;
                    y = (pose.r2 + pose.r4) / s; // [0][1] + [1][0]
                    z = (pose.r3 + pose.r7) / s; // [0][2] + [2][0]
                }
                else if (pose.r5 > pose.r9) // [1][1] > [2][2]
                {
                    double s = 2.0 * Math.Sqrt(1.0 + pose.r5 - pose.r1 - pose.r9); // 1.0 + [1][1] - [0][0] - [2][2]
                    w = (pose.r3 - pose.r7) / s; // [0][2] - [2][0]
                    x = (pose.r2 + pose.r4) / s; // [0][1] + [1][0]
                    y = 0.25 * s;
                    z = (pose.r6 + pose.r8) / s; // [1][2] + [2][1]
                }
                else
                {
                    double s = 2.0 * Math.Sqrt(1.0 + pose.r9 - pose.r1 - pose.r5); // (1.0 + [2][2] - [0][0] - [1][1]
                    w = (pose.r4 - pose.r2) / s; // [1][0] - R[0][1]
                    x = (pose.r3 + pose.r7) / s; // [0][2] + R[2][0]
                    y = (pose.r6 + pose.r8) / s; // [1][2] + R[2][1]
                    z = 0.25 * s;
                }
            }

            return new Quaternion
            {
                w = (float)w,
                x = (float)x,
                y = (float)y,
                z = (float)z,
            };
        }
    }
}
