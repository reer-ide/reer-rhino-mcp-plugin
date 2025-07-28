#if DEBUG
using System;
using Rhino;
using Rhino.Commands;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Commands
{
    /// <summary>
    /// Command to test the logging system and toggle debug logging
    /// </summary>
    public class TestLoggingCommand : Command
    {
        public TestLoggingCommand()
        {
            Instance = this;
        }

        /// <summary>
        /// The only instance of this command.
        /// </summary>
        public static TestLoggingCommand Instance { get; private set; }

        /// <summary>
        /// The command name as it appears on the Rhino command line.
        /// </summary>
        public override string EnglishName => "ReerTestLogging";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var plugin = ReerRhinoMCPPlugin.Instance;
                if (plugin == null)
                {
                    RhinoApp.WriteLine("MCP Plugin not loaded");
                    return Result.Failure;
                }

                RhinoApp.WriteLine("=== Testing Logging System ===");
                
                // Show current debug setting
                bool currentSetting = plugin.MCPSettings.EnableDebugLogging;
                RhinoApp.WriteLine($"Current debug logging: {currentSetting}");
                
                // Test all logging levels
                RhinoApp.WriteLine("\nTesting all logging levels:");
                RhinoApp.WriteLine("1. RhinoApp.WriteLine (always visible)");
                Logger.Debug("2. Logger.Debug (conditional)");
                Logger.Info("3. Logger.Info (conditional)");
                Logger.Success("4. Logger.Success (conditional)");
                Logger.Warning("5. Logger.Warning (always visible)");
                Logger.Error("6. Logger.Error (always visible)");
                
                RhinoApp.WriteLine("\nIf you only see messages 1, 5, and 6, debug logging is disabled.");
                RhinoApp.WriteLine("If you see all messages, debug logging is enabled.");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error testing logging: {ex.Message}");
                return Result.Failure;
            }
        }
    }

    /// <summary>
    /// Command to toggle debug logging on/off
    /// </summary>
    public class ToggleDebugLoggingCommand : Command
    {
        public ToggleDebugLoggingCommand()
        {
            Instance = this;
        }

        public static ToggleDebugLoggingCommand Instance { get; private set; }
        public override string EnglishName => "ReerToggleDebugLogging";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var plugin = ReerRhinoMCPPlugin.Instance;
                if (plugin == null)
                {
                    RhinoApp.WriteLine("MCP Plugin not loaded");
                    return Result.Failure;  
                }

                // Toggle the setting
                plugin.MCPSettings.EnableDebugLogging = !plugin.MCPSettings.EnableDebugLogging;
                
                // Save the settings
                plugin.MCPSettings.Save();
                
                RhinoApp.WriteLine($"Debug logging {(plugin.MCPSettings.EnableDebugLogging ? "ENABLED" : "DISABLED")}");
                RhinoApp.WriteLine("Run 'ReerTestLogging' to test the logging levels.");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error toggling debug logging: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
#endif