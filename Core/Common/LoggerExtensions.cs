using System;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Extension methods to help with logging migration
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Logs a message with automatic level detection based on content
        /// This helper method can be used during migration to automatically
        /// categorize existing log messages
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogSmart(this string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var lowerMessage = message.ToLowerInvariant();

            // Detect error messages
            if (lowerMessage.Contains("error") || 
                lowerMessage.Contains("failed") || 
                lowerMessage.Contains("exception") ||
                lowerMessage.Contains("✗") ||
                lowerMessage.StartsWith("✗"))
            {
                Logger.Error(message);
                return;
            }

            // Detect warning messages
            if (lowerMessage.Contains("warning") || 
                lowerMessage.Contains("warn") ||
                lowerMessage.Contains("⚠") ||
                lowerMessage.StartsWith("⚠"))
            {
                Logger.Warning(message);
                return;
            }

            // Detect success messages
            if (lowerMessage.Contains("success") || 
                lowerMessage.Contains("✓") ||
                lowerMessage.StartsWith("✓") ||
                lowerMessage.Contains("completed") ||
                lowerMessage.Contains("initialized"))
            {
                Logger.Success(message);
                return;
            }

            // Default to debug for most messages
            Logger.Debug(message);
        }

        /// <summary>
        /// Quick replacement for RhinoApp.WriteLine that uses smart logging
        /// Usage: Replace RhinoApp.WriteLine(message) with message.Log()
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Log(this string message)
        {
            message.LogSmart();
        }
    }
}