using System;
using Rhino;
using Rhino.Commands;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Commands
{
    public class RhinoMCPServerCommand : Command
    {
        public RhinoMCPServerCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RhinoMCPServerCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "RhinoReer";

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

            // Get user input for command
            string input = "";
            string prompt = isConnected ? 
                "Server is running. Commands: 'stop', 'status', or press Enter for status" :
                "Server stopped. Commands: 'local_start', 'status', or press Enter for status";
                
            if (Rhino.Input.RhinoGet.GetString(prompt, true, ref input) == Result.Success)
            {
                string command = input.ToLowerInvariant().Trim();
                
                switch (command)
                {
                    case "local_start":
                        if (isConnected)
                        {
                            RhinoApp.WriteLine("Server is already running. Use 'stop' first if you want to restart.");
                            break;
                        }
                        
                        RhinoApp.WriteLine("Starting local MCP server...");
                        var connectionSettings = settings.GetDefaultConnectionSettings();
                        connectionSettings.Mode = ConnectionMode.Local; // Ensure local mode
                        
                        var startTask = connectionManager.StartConnectionAsync(connectionSettings);
                        startTask.Wait(10000); // 10 second timeout
                        
                        if (connectionManager.IsConnected)
                        {
                            RhinoApp.WriteLine($"✓ Local MCP server started successfully on {connectionSettings.LocalHost}:{connectionSettings.LocalPort}");
                            RhinoApp.WriteLine("Ready for connections. You can now test with: python test_client.py");
                            return Result.Success;
                        }
                        else
                        {
                            RhinoApp.WriteLine("✗ Failed to start local MCP server");
                            return Result.Failure;
                        }
                        
                    case "stop":
                        if (!isConnected)
                        {
                            RhinoApp.WriteLine("Server is not running.");
                            break;
                        }
                        
                        RhinoApp.WriteLine("Stopping MCP server...");
                        var stopTask = connectionManager.StopConnectionAsync();
                        stopTask.Wait(5000);
                        
                        if (!connectionManager.IsConnected)
                        {
                            RhinoApp.WriteLine("✓ MCP server stopped successfully");
                            return Result.Success;
                        }
                        else
                        {
                            RhinoApp.WriteLine("✗ Failed to stop MCP server");
                            return Result.Failure;
                        }
                        
                    case "status":
                    case "":
                        // Show status (handled below)
                        break;
                        
                    default:
                        RhinoApp.WriteLine($"Unknown command: '{input}'");
                        RhinoApp.WriteLine("Available commands: local_start, stop, status");
                        break;
                }
            }

            // Show current status
            ShowStatus(connectionManager, settings);
            return Result.Success;
        }

        private void ShowStatus(IConnectionManager connectionManager, ReerRhinoMCPPlugin.Config.RhinoMCPSettings settings)
        {
            RhinoApp.WriteLine("=== RhinoReer MCP Server Status ===");
            RhinoApp.WriteLine($"Status: {connectionManager.Status}");
            RhinoApp.WriteLine($"Connected: {(connectionManager.IsConnected ? "✓ YES" : "✗ NO")}");
            
            if (settings != null)
            {
                var defaultSettings = settings.GetDefaultConnectionSettings();
                RhinoApp.WriteLine($"Mode: {defaultSettings.Mode}");
                RhinoApp.WriteLine($"Host: {defaultSettings.LocalHost}");
                RhinoApp.WriteLine($"Port: {defaultSettings.LocalPort}");
                RhinoApp.WriteLine($"Auto-start: {settings.AutoStart}");
                RhinoApp.WriteLine($"Debug logging: {settings.EnableDebugLogging}");
            }
            
            RhinoApp.WriteLine("");
            if (connectionManager.IsConnected)
            {
                RhinoApp.WriteLine("🔗 Server is running and ready for connections");
                RhinoApp.WriteLine("   Test with: python test_client.py");
            }
            else
            {
                RhinoApp.WriteLine("💤 Server is stopped");
                RhinoApp.WriteLine("   Start with: RhinoReer → local_start");
            }
            RhinoApp.WriteLine("=====================================");
        }
    }
} 