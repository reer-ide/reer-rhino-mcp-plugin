using System;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Client;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin;

namespace ReerRhinoMCPPlugin.Commands
{
    public class ReerStartCommand : Command
    {
        public ReerStartCommand() { Instance = this; }
        public static ReerStartCommand Instance { get; private set; }
        public override string EnglishName => "ReerStart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;

            try
            {
                // Handle menu selection on main thread
                var option = ShowStartMenu();
                switch (option)
                {
                    case StartMenuOption.Local:
                        return HandleStartLocal(connectionManager);
                    case StartMenuOption.Remote:
                        return HandleStartRemote(connectionManager);
                    default:
                        return Result.Success;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred: {ex.Message}");
                return Result.Failure;
            }
        }

        private StartMenuOption ShowStartMenu()
        {
            var getter = new GetOption();
            getter.SetCommandPrompt("RhinoMCP Start: Select connection type");
            getter.AddOption("Local", "Start local TCP server (for Claude Desktop)");
            getter.AddOption("Remote", "Connect to remote MCP server");
            getter.AddOption("Cancel", "Return to main menu");
            
            var result = getter.Get();

            if (result != GetResult.Option)
                return StartMenuOption.Cancel;

            var selectedOption = getter.Option().EnglishName;
            switch (selectedOption)
            {
                case "Local": return StartMenuOption.Local;
                case "Remote": return StartMenuOption.Remote;
                default: return StartMenuOption.Cancel;
            }
        }

        private Result HandleStartLocal(IConnectionManager connectionManager)
        {
            // Collect inputs on main thread first
            RhinoApp.WriteLine("=== Starting Local TCP Server ===");
            
            var host = GetUserInput("Enter host (default: 127.0.0.1):", "127.0.0.1");
            if (string.IsNullOrEmpty(host))
            {
                RhinoApp.WriteLine("Local server start cancelled.");
                return Result.Cancel;
            }
            
            var portStr = GetUserInput("Enter port (default: 1999):", "1999");
            if (string.IsNullOrEmpty(portStr))
            {
                RhinoApp.WriteLine("Local server start cancelled.");
                return Result.Cancel;
            }
            
            if (!int.TryParse(portStr, out int port)) 
            {
                port = 1999;
                RhinoApp.WriteLine($"Invalid port number, using default: {port}");
            }

            // Confirm settings before starting
            RhinoApp.WriteLine($"\n=== Server Configuration ===");
            RhinoApp.WriteLine($"Host: {host}");
            RhinoApp.WriteLine($"Port: {port}");
            
            var confirm = GetUserInput("Start server with these settings? (yes/no):", "yes");
            if (confirm?.ToLower() != "yes")
            {
                RhinoApp.WriteLine("Local server start cancelled.");
                return Result.Cancel;
            }

            // Now run async operation in background
            Task.Run(async () => await RunStartLocalAsync(connectionManager, host, port));
            return Result.Success;
        }

        private async Task<bool> RunStartLocalAsync(IConnectionManager connectionManager, string host, int port)
        {
            try
            {
                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalHost = host,
                    LocalPort = port
                };
                
                RhinoApp.WriteLine("Starting local TCP server...");
                var success = await connectionManager.StartConnectionAsync(settings);
                if (success)
                {
                    RhinoApp.WriteLine($"✓ Local TCP server started successfully on {host}:{port}.");
                    
                    // Save settings for future auto-start and restart
                    var pluginSettings = ReerRhinoMCPPlugin.Instance.MCPSettings;
                    pluginSettings.DefaultConnection = settings;
                    pluginSettings.LastUsedMode = ConnectionMode.Local;
                    pluginSettings.Save();
                    RhinoApp.WriteLine("Settings saved for auto-start and restart");
                }
                else
                {
                    Logger.Error("✗ Failed to start local TCP server.");
                }
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting local server: {ex.Message}");
                return false;
            }
        }

        private Result HandleStartRemote(IConnectionManager connectionManager)
        {
            RhinoApp.WriteLine("=== Connecting to Remote MCP Server ===");
            RhinoApp.WriteLine("Checking license and connecting...");
            
            // Run async operation in background
            Task.Run(async () => await RunStartRemoteAsync(connectionManager));
            return Result.Success;
        }

        private async Task<bool> RunStartRemoteAsync(IConnectionManager connectionManager)
        {
            try
            {
                var remoteClient = new RhinoMCPClient();
                var licenseResult = await remoteClient.GetLicenseStatusAsync();

                if (!licenseResult.IsValid)
                {
                    Logger.Error("✗ No valid license found. Please run 'ReerLicense' to register first.");
                    return false;
                }

                var storedLicense = await new LicenseManager().GetStoredLicenseInfoAsync();
                var serverUrl = storedLicense?.ServerUrl;
                if(string.IsNullOrEmpty(serverUrl))
                {
                    Logger.Error("✗ No server URL found in stored license. Please re-register your license with 'ReerLicense'.");
                    return false;
                }

                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Remote,
                    RemoteUrl = serverUrl
                };

                Logger.Info($"Connecting to: {serverUrl} with License: {licenseResult.LicenseId}");
                var success = await connectionManager.StartConnectionAsync(settings);

                if (success)
                {
                    RhinoApp.WriteLine("✓ Remote connection established successfully!");
                    
                    // Save settings for future auto-start and restart
                    var pluginSettings = ReerRhinoMCPPlugin.Instance.MCPSettings;
                    pluginSettings.DefaultConnection = settings;
                    pluginSettings.LastUsedMode = ConnectionMode.Remote;
                    pluginSettings.Save();
                    RhinoApp.WriteLine("Settings saved for auto-start and restart");
                }
                else
                {
                    Logger.Error("✗ Failed to establish remote connection.");
                }
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting remote connection: {ex.Message}");
                return false;
            }
        }
        
        private string GetUserInput(string prompt, string defaultValue = null)
        {
            var getter = new GetString();
            getter.SetCommandPrompt(prompt);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                getter.SetDefaultString(defaultValue);
            }
            return getter.Get() == GetResult.String ? getter.StringResult() : null;
        }

        private enum StartMenuOption { Local, Remote, Cancel }
    }
}
