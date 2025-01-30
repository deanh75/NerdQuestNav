using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class AprilTagLibraryLoader
{
    // Import the dummy function from the AprilTag native library
    [DllImport("AprilTag")]
    private static extern int apriltag_test_function_wrapper();

    public static void LoadLibrary()
    {
        try
        {
            int result = apriltag_test_function_wrapper();
            Debug.Log($"[AprilTag] Library loaded successfully. Test function returned: {result} | Expected: 42");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AprilTag] Failed to load native library: {e.Message}");
        }
    }
}
