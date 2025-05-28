using System;
using Rhino;
using Rhino.Commands;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Commands
{
    public class RhinoMCPCommand : Command
    {
        public RhinoMCPCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RhinoMCPCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "RhinoMCP";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
            if (plugin == null)
            {
                RhinoApp.WriteLine("RhinoMCP plugin not loaded");
                return Result.Failure;
            }

            var connectionManager = plugin.ConnectionManager;
            var settings = plugin.MCPSettings;

            if (connectionManager == null)
            {
                RhinoApp.WriteLine("Connection manager not available");
                return Result.Failure;
            }

            // Check current status
            bool isConnected = connectionManager.IsConnected;
            var status = connectionManager.Status;

            RhinoApp.WriteLine($"Current MCP Status: {status}");

            if (isConnected)
            {
                // Server is running, ask if user wants to stop it
                RhinoApp.WriteLine("MCP Server is running. Type 'stop' to stop it, or press Enter to show status.");
                
                string input = "";
                if (Rhino.Input.RhinoGet.GetString("Enter command (stop/status)", true, ref input) == Result.Success)
                {
                    if (input.ToLowerInvariant() == "stop")
                    {
                        RhinoApp.WriteLine("Stopping MCP server...");
                        var stopTask = connectionManager.StopConnectionAsync();
                        stopTask.Wait(5000);
                        
                        if (!connectionManager.IsConnected)
                        {
                            RhinoApp.WriteLine("MCP server stopped successfully");
                            return Result.Success;
                        }
                        else
                        {
                            RhinoApp.WriteLine("Failed to stop MCP server");
                            return Result.Failure;
                        }
                    }
                }

                // Show current status
                ShowStatus(connectionManager, settings);
                return Result.Success;
            }
            else
            {
                // Server is not running, ask if user wants to start it
                string input = "";
                if (Rhino.Input.RhinoGet.GetString("MCP Server is not running. Type 'start' to start it", true, ref input) == Result.Success)
                {
                    if (input.ToLowerInvariant() == "start")
                    {
                        RhinoApp.WriteLine("Starting MCP server...");
                        
                        var connectionSettings = settings.GetDefaultConnectionSettings();
                        var startTask = connectionManager.StartConnectionAsync(connectionSettings);
                        startTask.Wait(10000); // 10 second timeout
                        
                        if (connectionManager.IsConnected)
                        {
                            RhinoApp.WriteLine($"MCP server started successfully on {connectionSettings.LocalHost}:{connectionSettings.LocalPort}");
                            RhinoApp.WriteLine("You can now test it with the Python test client: python test_client.py");
                            return Result.Success;
                        }
                        else
                        {
                            RhinoApp.WriteLine("Failed to start MCP server");
                            return Result.Failure;
                        }
                    }
                }

                ShowStatus(connectionManager, settings);
                return Result.Success;
            }
        }

        private void ShowStatus(IConnectionManager connectionManager, ReerRhinoMCPPlugin.Config.RhinoMCPSettings settings)
        {
            RhinoApp.WriteLine("=== RhinoMCP Status ===");
            RhinoApp.WriteLine($"Status: {connectionManager.Status}");
            RhinoApp.WriteLine($"Connected: {connectionManager.IsConnected}");
            
            if (settings != null)
            {
                var defaultSettings = settings.GetDefaultConnectionSettings();
                RhinoApp.WriteLine($"Mode: {defaultSettings.Mode}");
                RhinoApp.WriteLine($"Host: {defaultSettings.LocalHost}");
                RhinoApp.WriteLine($"Port: {defaultSettings.LocalPort}");
                RhinoApp.WriteLine($"Auto-start: {settings.AutoStart}");
                RhinoApp.WriteLine($"Debug logging: {settings.EnableDebugLogging}");
            }
            
            RhinoApp.WriteLine("======================");
        }
    }
} 