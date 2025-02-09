using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class to prevent spamming the console with repeated log messages or exceptions.
/// Each unique log or exception is throttled to appear at most once every THROTTLE_INTERVAL seconds.
/// </summary>
public static class ThrottleLogger
{
    // Stores the last time (in seconds since application start) we logged a particular key
    private static Dictionary<string, float> lastLogTime = new Dictionary<string, float>();

    // Throttle interval in seconds
    private const float THROTTLE_INTERVAL = 1.0f;

    /// <summary>
    /// Logs a message at most once per throttle interval.
    /// </summary>
    public static void Log(string message)
    {
        if (CanLog(message))
        {
            Debug.Log(message);
        }
    }

    /// <summary>
    /// Logs a warning at most once per throttle interval.
    /// </summary>
    public static void LogWarning(string message)
    {
        if (CanLog(message))
        {
            Debug.LogWarning(message);
        }
    }

    /// <summary>
    /// Logs an error at most once per throttle interval.
    /// </summary>
    public static void LogError(string message)
    {
        if (CanLog(message))
        {
            Debug.LogError(message);
        }
    }

    /// <summary>
    /// Logs an exception at most once per throttle interval.
    /// This replicates Debug.LogException but throttles repeated identical exceptions.
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="context">An optional UnityEngine.Object context</param>
    public static void LogException(Exception exception, UnityEngine.Object context = null)
    {
        if (exception == null) return;

        // Use the exception Message + StackTrace as the unique key
        // so repeated identical exceptions are throttled.
        string key = exception.Message + "\n" + exception.StackTrace;

        if (CanLog(key))
        {
            // You can also pass a context if needed.
            Debug.LogException(exception, context);
        }
    }

    /// <summary>
    /// Returns true if the given key can be logged now (i.e., at least THROTTLE_INTERVAL seconds have passed).
    /// </summary>
    private static bool CanLog(string key)
    {
        float currentTime = Time.time;
        if (!lastLogTime.ContainsKey(key) || (currentTime - lastLogTime[key]) >= THROTTLE_INTERVAL)
        {
            lastLogTime[key] = currentTime;
            return true;
        }
        return false;
    }
}
