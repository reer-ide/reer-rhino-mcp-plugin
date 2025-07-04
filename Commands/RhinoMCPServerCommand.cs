using System;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;
using ReerRhinoMCPPlugin.UI;

namespace ReerRhinoMCPPlugin.Commands
{
    public class RhinoMCPServerCommand : Command
    {
        public RhinoMCPServerCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a reference in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RhinoMCPServerCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "RhinoReer";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
                if (plugin == null)
                {
                    RhinoApp.WriteLine("ERROR: ReerRhinoMCPPlugin not loaded");
                    return Result.Failure;
                }

                // Get user input for operation
                var go = new Rhino.Input.Custom.GetOption();
                go.SetCommandPrompt("Choose MCP operation");
                go.AddOption("local_start");
                go.AddOption("local_stop");
                go.AddOption("status");
                go.AddOption("ui");
                go.AddOption("settings");

                var result = go.Get();
                if (result != Rhino.Input.GetResult.Option)
                {
                    return Result.Cancel;
                }

                var option = go.Option().EnglishName;
                RhinoApp.WriteLine($"Selected option: {option}");

                switch (option.ToLower())
                {
                    case "local_start":
                        return HandleLocalStart(plugin);
                    case "local_stop":
                        return HandleLocalStop(plugin);
                    case "status":
                        return HandleStatus(plugin);
                    case "ui":
                        return HandleUI(plugin);
                    case "settings":
                        return HandleSettings(plugin);
                    default:
                        RhinoApp.WriteLine($"Unknown option: {option}");
                        return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in RhinoReer command: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result HandleLocalStart(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                if (plugin.ConnectionManager.IsConnected)
                {
                    RhinoApp.WriteLine("MCP server is already running");
                    return Result.Success;
                }

                RhinoApp.WriteLine("Starting MCP server...");
                
                var connectionSettings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalPort = plugin.MCPSettings.DefaultConnection.LocalPort
                };

                var startTask = plugin.ConnectionManager.StartConnectionAsync(connectionSettings);
                
                // Wait for completion with timeout
                if (startTask.Wait(5000))
                {
                    if (startTask.Result)
                    {
                        RhinoApp.WriteLine($"✓ MCP server started successfully on port {connectionSettings.LocalPort}");
                        RhinoApp.WriteLine("You can now connect clients to this server");
                        return Result.Success;
                    }
                    else
                    {
                        RhinoApp.WriteLine("✗ Failed to start MCP server");
                        return Result.Failure;
                    }
                }
                else
                {
                    RhinoApp.WriteLine("✗ Timeout starting MCP server");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting server: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result HandleLocalStop(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                if (!plugin.ConnectionManager.IsConnected)
                {
                    RhinoApp.WriteLine("MCP server is not running");
                    return Result.Success;
                }

                RhinoApp.WriteLine("Stopping MCP server...");
                
                var stopTask = plugin.ConnectionManager.StopConnectionAsync();
                
                // Wait for completion with timeout
                if (stopTask.Wait(5000))
                {
                    RhinoApp.WriteLine("✓ MCP server stopped successfully");
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("✗ Timeout stopping MCP server");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error stopping server: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result HandleStatus(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                var connectionManager = plugin.ConnectionManager;
                
                RhinoApp.WriteLine("=== MCP Server Status ===");
                RhinoApp.WriteLine($"Status: {connectionManager.Status}");
                RhinoApp.WriteLine($"Connected: {connectionManager.IsConnected}");
                
                if (connectionManager.IsConnected)
                {
                    RhinoApp.WriteLine($"Port: {plugin.MCPSettings.DefaultConnection.LocalPort}");
                    RhinoApp.WriteLine($"Client Count: {connectionManager.ClientCount}");
                }
                
                RhinoApp.WriteLine($"Auto-start: {plugin.MCPSettings.AutoStart}");
                RhinoApp.WriteLine($"Debug Logging: {plugin.MCPSettings.EnableDebugLogging}");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting status: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result HandleUI(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                RhinoApp.WriteLine("Opening MCP Control Panel...");
                plugin.ShowControlPanel();
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error opening UI: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result HandleSettings(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                var settings = plugin.MCPSettings;
                
                RhinoApp.WriteLine("=== MCP Settings ===");
                RhinoApp.WriteLine($"Default Port: {settings.DefaultConnection.LocalPort}");
                RhinoApp.WriteLine($"Auto-start: {settings.AutoStart}");
                RhinoApp.WriteLine($"Debug Logging: {settings.EnableDebugLogging}");
                RhinoApp.WriteLine($"Show Status Bar: {settings.ShowStatusBar}");
                
                RhinoApp.WriteLine("Use 'RhinoReer → ui' to modify settings");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error displaying settings: {ex.Message}");
                return Result.Failure;
            }
        }
    }
} 