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

            Task.Run(async () =>
            {
                try
                {
                    var option = ShowStartMenu();
                    switch (option)
                    {
                        case StartMenuOption.Local:
                            await RunStartLocal(connectionManager);
                            break;
                        case StartMenuOption.Remote:
                            await RunStartRemote(connectionManager);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"An error occurred: {ex.Message}");
                }
            });

            return Result.Success;
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

        private async Task<bool> RunStartLocal(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Starting Local TCP Server ===");
                var host = GetUserInput("Enter host (default: 127.0.0.1):", "127.0.0.1");
                var portStr = GetUserInput("Enter port (default: 1999):", "1999");
                if (!int.TryParse(portStr, out int port)) port = 1999;

                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalHost = host,
                    LocalPort = port
                };
                
                var success = await connectionManager.StartConnectionAsync(settings);
                if (success)
                {
                    RhinoApp.WriteLine($"✓ Local TCP server started successfully on {host}:{port}.");
                }
                else
                {
                    RhinoApp.WriteLine("✗ Failed to start local TCP server.");
                }
                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting local server: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunStartRemote(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Connecting to Remote MCP Server ===");
                var remoteClient = new RhinoMCPClient();
                var licenseResult = await remoteClient.GetLicenseStatusAsync();

                if (!licenseResult.IsValid)
                {
                    RhinoApp.WriteLine("✗ No valid license found. Please run 'ReerLicense' to register first.");
                    return false;
                }

                var storedLicense = await new LicenseManager().GetStoredLicenseInfoAsync();
                var serverUrl = storedLicense?.ServerUrl;
                if(string.IsNullOrEmpty(serverUrl))
                {
                    serverUrl = GetUserInput("Enter remote MCP server URL:", "https://rhinomcp.your-server.com");
                    if (string.IsNullOrEmpty(serverUrl)) return true;
                }

                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Remote,
                    RemoteUrl = serverUrl
                };

                RhinoApp.WriteLine($"Connecting to: {serverUrl} with License: {licenseResult.LicenseId}");
                var success = await connectionManager.StartConnectionAsync(settings);

                if (success)
                {
                    RhinoApp.WriteLine("✓ Remote connection established successfully!");
                }
                else
                {
                    RhinoApp.WriteLine("✗ Failed to establish remote connection.");
                }
                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting remote connection: {ex.Message}");
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
