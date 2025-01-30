using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class AprilTagLibraryLoader
{
    // Import the dummy function from the AprilTag native library
    [DllImport("libapriltag.so")]
    private static extern int apriltag_test_function_wrapper();

    public static void LoadLibrary()
    {
        Debug.Log($"[AprilTag] Attempting to load the AprilTag library.");
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
