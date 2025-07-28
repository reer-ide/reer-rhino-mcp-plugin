using System;
using Rhino;
using ReerRhinoMCPPlugin.Config;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Centralized logging utility that respects debug settings
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// Log levels for different types of messages
        /// </summary>
        public enum Level
        {
            Debug,      // Only shown when debug logging is enabled
            Info,       // Only shown when debug logging is enabled
            Warning,    // Always shown
            Error,      // Always shown
            Success     // Only shown when debug logging is enabled
        }

        /// <summary>
        /// Logs a debug message (only visible when debug logging is enabled)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Debug(string message)
        {
            Log(Level.Debug, message);
        }

        /// <summary>
        /// Logs an info message (only visible when debug logging is enabled)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Info(string message)
        {
            Log(Level.Info, message);
        }

        /// <summary>
        /// Logs a warning message (always visible)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Warning(string message)
        {
            Log(Level.Warning, message);
        }

        /// <summary>
        /// Logs an error message (always visible)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Error(string message)
        {
            Log(Level.Error, message);
        }

        /// <summary>
        /// Logs a success message (only visible when debug logging is enabled)
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Success(string message)
        {
            Log(Level.Success, message);
        }

        /// <summary>
        /// Main logging method that respects debug settings
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="message">The message to log</param>
        private static void Log(Level level, string message)
        {
            try
            {
                // Get current debug logging setting
                bool debugEnabled = IsDebugLoggingEnabled();

                // Determine if message should be shown
                bool shouldLog = level switch
                {
                    Level.Warning => true,  // Always show warnings
                    Level.Error => true,    // Always show errors
                    Level.Debug => debugEnabled,    // Only show debug messages when enabled
                    Level.Info => debugEnabled,     // Only show info messages when enabled
                    Level.Success => debugEnabled,  // Only show success messages when enabled
                    _ => debugEnabled
                };

                if (!shouldLog) return;

                // Format message with level prefix
                string formattedMessage = level switch
                {
                    Level.Debug => $"[DEBUG] {message}",
                    Level.Info => $"[INFO] {message}",
                    Level.Warning => $"[WARNING] {message}",
                    Level.Error => $"[ERROR] {message}",
                    Level.Success => $"[SUCCESS] {message}",
                    _ => message
                };

                // Output to Rhino command line
                RhinoApp.WriteLine(formattedMessage);

                // Also add to UI log if available
                AddToUILog(level, message);
            }
            catch (Exception ex)
            {
                // Fallback logging in case of errors
                RhinoApp.WriteLine($"[LOGGER ERROR] Failed to log message: {ex.Message}");
                RhinoApp.WriteLine($"[FALLBACK] {message}");
            }
        }

        /// <summary>
        /// Gets the current debug logging setting
        /// </summary>
        /// <returns>True if debug logging is enabled, false otherwise</returns>
        private static bool IsDebugLoggingEnabled()
        {
            try
            {
                var plugin = ReerRhinoMCPPlugin.Instance;
                return plugin?.MCPSettings?.EnableDebugLogging ?? false;
            }
            catch
            {
                // Default to false if settings can't be accessed
                return false;
            }
        }

        /// <summary>
        /// Adds message to UI log viewer if available
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="message">The message to log</param>
        private static void AddToUILog(Level level, string message)
        {
            try
            {
                var plugin = ReerRhinoMCPPlugin.Instance;
                var connectionManager = plugin?.ConnectionManager;

                // Try to get the UI log from connection manager or other UI components
                // This will need to be implemented based on how the UI logging system works
                // For now, we'll leave this as a placeholder for future integration
                
                // TODO: Integrate with LogViewModel when available
            }
            catch
            {
                // Silently ignore UI logging errors
            }
        }

        /// <summary>
        /// Logs a formatted message with parameters (debug level)
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void DebugFormat(string format, params object[] args)
        {
            Debug(string.Format(format, args));
        }

        /// <summary>
        /// Logs a formatted message with parameters (info level)
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void InfoFormat(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        /// <summary>
        /// Logs a formatted message with parameters (warning level)
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void WarningFormat(string format, params object[] args)
        {
            Warning(string.Format(format, args));
        }

        /// <summary>
        /// Logs a formatted message with parameters (error level)
        /// </summary>
        /// <param name="format">The format string</param>
        /// <param name="args">The format arguments</param>
        public static void ErrorFormat(string format, params object[] args)
        {
            Error(string.Format(format, args));
        }
    }
}