using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;
using ReerRhinoMCPPlugin.UI;
using rhino_mcp_plugin;

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

        public static RhinoMCPServerCommand Instance { get; private set; }

        public override string EnglishName => "RhinoMCP";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
            var connectionManager = plugin.ConnectionManager;

            try
            {
                // Show main menu
                var option = ShowMainMenu(connectionManager);
                
                switch (option)
                {
                    case MainMenuOption.RegisterLicense:
                        return RunRegisterLicense(connectionManager);
                        
                    case MainMenuOption.CheckLicense:
                        return RunCheckLicense(connectionManager);
                        
                    case MainMenuOption.StartLocal:
                        return RunStartLocal(connectionManager);
                        
                    case MainMenuOption.StartRemote:
                        return RunStartRemote(connectionManager);
                        
                    case MainMenuOption.Stop:
                        return RunStop(connectionManager);
                        
                    case MainMenuOption.Status:
                        return RunStatus(connectionManager);
                        
                    case MainMenuOption.CheckFiles:
                        return RunCheckFiles(connectionManager);
                        
                    case MainMenuOption.ClearFiles:
                        return RunClearFiles(connectionManager);
                        
                    case MainMenuOption.ClearLicense:
                        return RunClearLicense(connectionManager);
                        
                    case MainMenuOption.Cancel:
                        return Result.Cancel;
                        
                    default:
                        return Result.Nothing;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error in RhinoMCP command: {ex.Message}");
                return Result.Failure;
            }
        }

        private MainMenuOption ShowMainMenu(IConnectionManager connectionManager)
        {
            var getter = new GetOption();
            getter.SetCommandPrompt("RhinoMCP: Select an option");
            
            // Add options based on current state
            getter.AddOption("RegisterLicense", "Register a new license with remote server");
            getter.AddOption("CheckLicense", "Check current license status");
            getter.AddOption("StartLocal", "Start local TCP server (for Claude Desktop)");
            getter.AddOption("StartRemote", "Connect to remote MCP server");
            getter.AddOption("Stop", "Stop current connection");
            getter.AddOption("Status", "Show connection status");
            getter.AddOption("CheckFiles", "Check status of linked files");
            getter.AddOption("ClearFiles", "Clear all linked files (troubleshooting)");
            getter.AddOption("ClearLicense", "Clear stored license (troubleshooting)");
            
            var result = getter.Get();
            
            if (result != GetResult.Option)
                return MainMenuOption.Cancel;
                
            var selectedOption = getter.Option().EnglishName;
            
            switch (selectedOption)
            {
                case "RegisterLicense": return MainMenuOption.RegisterLicense;
                case "CheckLicense": return MainMenuOption.CheckLicense;
                case "StartLocal": return MainMenuOption.StartLocal;
                case "StartRemote": return MainMenuOption.StartRemote;
                case "Stop": return MainMenuOption.Stop;
                case "Status": return MainMenuOption.Status;
                case "CheckFiles": return MainMenuOption.CheckFiles;
                case "ClearFiles": return MainMenuOption.ClearFiles;
                case "ClearLicense": return MainMenuOption.ClearLicense;
                default: return MainMenuOption.Cancel;
            }
        }

        private Result RunRegisterLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Registration ===");
                RhinoApp.WriteLine("This is a one-time setup to register your license with the remote MCP server.");
                RhinoApp.WriteLine("");
                
                // Get license key from user
                var licenseKey = GetUserInput("Enter your license key:");
                if (string.IsNullOrEmpty(licenseKey))
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }
                
                // Get user ID from user
                var userId = GetUserInput("Enter your user ID:");
                if (string.IsNullOrEmpty(userId))
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }
                
                // Get server URL from user
                var serverUrl = GetUserInput("Enter remote MCP server URL:", "https://rhinomcp.your-server.com");
                if (string.IsNullOrEmpty(serverUrl))
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }
                
                // Show machine fingerprint for user awareness
                var displayFingerprint = Core.Client.MachineFingerprinting.GetDisplayFingerprint();
                RhinoApp.WriteLine($"Machine fingerprint: {displayFingerprint}");
                RhinoApp.WriteLine("This will be used to bind the license to this machine.");
                RhinoApp.WriteLine("");
                RhinoApp.WriteLine("Registering license... (this may take a few seconds)");
                
                // Perform registration on background thread to avoid blocking UI
                var success = Task.Run(async () => await RegisterLicenseAsync(licenseKey, userId, serverUrl)).GetAwaiter().GetResult();
                
                if (success)
                {
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine("✓ License registration completed successfully!");
                    RhinoApp.WriteLine("You can now use 'StartRemote' to connect to the remote MCP server.");
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("");
                    RhinoApp.WriteLine("✗ License registration failed. Please check your license key and try again.");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during license registration: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunCheckLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Status ===");
                
                // Run on background thread to avoid blocking UI
                var licenseResult = Task.Run(async () => await CheckLicenseAsync()).GetAwaiter().GetResult();
                
                if (licenseResult.IsValid)
                {
                    RhinoApp.WriteLine("✓ License is valid and active");
                    RhinoApp.WriteLine($"  License ID: {licenseResult.LicenseId}");
                    RhinoApp.WriteLine($"  User ID: {licenseResult.UserId}");
                    RhinoApp.WriteLine($"  Tier: {licenseResult.Tier}");
                    RhinoApp.WriteLine($"  Max concurrent files: {licenseResult.MaxConcurrentFiles}");
                    
                    var displayFingerprint = Core.Client.MachineFingerprinting.GetDisplayFingerprint();
                    RhinoApp.WriteLine($"  Machine fingerprint: {displayFingerprint}");
                }
                else
                {
                    RhinoApp.WriteLine("✗ License validation failed");
                    RhinoApp.WriteLine($"  Reason: {licenseResult.Message}");
                    RhinoApp.WriteLine("  Use 'RegisterLicense' to register a new license.");
                }
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error checking license: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunStartLocal(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Starting Local TCP Server ===");
                
                var host = GetUserInput("Enter host (default: 127.0.0.1):", "127.0.0.1");
                if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
                
                var portStr = GetUserInput("Enter port (default: 1999):", "1999");
                if (string.IsNullOrEmpty(portStr)) portStr = "1999";
                
                if (!int.TryParse(portStr, out int port))
                {
                    RhinoApp.WriteLine("Invalid port number. Using default: 1999");
                    port = 1999;
                }
                
                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalHost = host,
                    LocalPort = port
                };
                
                // Run on background thread to avoid blocking UI
                var success = Task.Run(async () => await connectionManager.StartConnectionAsync(settings)).GetAwaiter().GetResult();
                
                if (success)
                {
                    RhinoApp.WriteLine($"✓ Local TCP server started successfully on {host}:{port}");
                    RhinoApp.WriteLine("Ready to accept connections from Claude Desktop or other MCP clients.");
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("✗ Failed to start local TCP server");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting local server: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunStartRemote(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Connecting to Remote MCP Server ===");
                
                // First check if license is valid - run on background thread
                var licenseResult = Task.Run(async () => await CheckLicenseAsync()).GetAwaiter().GetResult();
                
                if (!licenseResult.IsValid)
                {
                    RhinoApp.WriteLine("✗ No valid license found. Please register a license first.");
                    RhinoApp.WriteLine("  Use 'RegisterLicense' to register your license.");
                    return Result.Failure;
                }
                
                // Get server URL from stored license or prompt user
                var serverUrl = GetServerUrlFromLicense();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    serverUrl = GetUserInput("Enter remote MCP server URL:", "https://rhinomcp.your-server.com");
                    if (string.IsNullOrEmpty(serverUrl))
                    {
                        RhinoApp.WriteLine("Remote connection cancelled.");
                        return Result.Cancel;
                    }
                }
                
                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Remote,
                    RemoteUrl = serverUrl
                };
                
                RhinoApp.WriteLine($"Connecting to: {serverUrl}");
                RhinoApp.WriteLine($"License: {licenseResult.LicenseId} (Tier: {licenseResult.Tier})");
                RhinoApp.WriteLine("Establishing connection... (this may take a few seconds)");
                
                // Run on background thread to avoid blocking UI
                var success = Task.Run(async () => await connectionManager.StartConnectionAsync(settings)).GetAwaiter().GetResult();
                
                if (success)
                {
                    RhinoApp.WriteLine($"✓ Remote connection established successfully!");
                    RhinoApp.WriteLine("Ready to receive CAD commands from host applications.");
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("✗ Failed to establish remote connection");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting remote connection: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunStop(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Stopping Connection ===");
                
                if (!connectionManager.IsConnected)
                {
                    RhinoApp.WriteLine("No active connection to stop.");
                    return Result.Nothing;
                }
                
                // Run on background thread to avoid blocking UI
                Task.Run(async () => await connectionManager.StopConnectionAsync()).GetAwaiter().GetResult();
                
                RhinoApp.WriteLine("✓ Connection stopped successfully");
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error stopping connection: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunStatus(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP Status ===");
                
                // Connection status
                RhinoApp.WriteLine($"Connection Status: {connectionManager.Status}");
                RhinoApp.WriteLine($"Is Connected: {connectionManager.IsConnected}");
                
                if (connectionManager.ActiveConnection != null)
                {
                    var settings = connectionManager.ActiveConnection.Settings;
                    RhinoApp.WriteLine($"Connection Mode: {settings.Mode}");
                    
                    if (settings.Mode == ConnectionMode.Local)
                    {
                        RhinoApp.WriteLine($"Local Server: {settings.LocalHost}:{settings.LocalPort}");
                    }
                    else
                    {
                        RhinoApp.WriteLine($"Remote Server: {settings.RemoteUrl}");
                    }
                }
                
                // License status
                var licenseTask = CheckLicenseAsync();
                var licenseResult = licenseTask.GetAwaiter().GetResult();
                
                RhinoApp.WriteLine($"License Status: {(licenseResult.IsValid ? "Valid" : "Invalid")}");
                if (licenseResult.IsValid)
                {
                    RhinoApp.WriteLine($"License ID: {licenseResult.LicenseId}");
                    RhinoApp.WriteLine($"Tier: {licenseResult.Tier}");
                }
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting status: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunCheckFiles(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Checking Linked Files ===");
                
                var remoteClient = GetRemoteClient(connectionManager);
                if (remoteClient != null)
                {
                    // Get all linked files
                    var linkedFiles = remoteClient.GetLinkedFiles();
                    
                    if (!linkedFiles.Any())
                    {
                        RhinoApp.WriteLine("No linked files found.");
                        return Result.Success;
                    }
                    
                    RhinoApp.WriteLine($"Found {linkedFiles.Count} linked file(s):");
                    RhinoApp.WriteLine("");
                    
                    foreach (var file in linkedFiles)
                    {
                        RhinoApp.WriteLine($"Session: {file.SessionId}");
                        RhinoApp.WriteLine($"  File: {Path.GetFileName(file.FilePath)}");
                        RhinoApp.WriteLine($"  Full Path: {file.FilePath}");
                        RhinoApp.WriteLine($"  Status: {file.Status}");
                        RhinoApp.WriteLine($"  Size: {file.FileSize:N0} bytes");
                        RhinoApp.WriteLine($"  Registered: {file.RegisteredAt:yyyy-MM-dd HH:mm:ss}");
                        RhinoApp.WriteLine($"  Last Modified: {file.LastModified:yyyy-MM-dd HH:mm:ss}");
                        RhinoApp.WriteLine("");
                    }
                    
                    // Check for file status changes
                    var validateTask = remoteClient.ValidateLinkedFilesAsync();
                    var statusChanges = validateTask.GetAwaiter().GetResult();
                    
                    if (statusChanges.Any())
                    {
                        RhinoApp.WriteLine("File status changes detected:");
                        foreach (var change in statusChanges)
                        {
                            RhinoApp.WriteLine($"  • {change.Message}");
                        }
                        
                        // Report changes to server if connected
                        if (connectionManager.IsConnected)
                        {
                            var reportTask = remoteClient.ReportFileStatusChangesAsync(statusChanges);
                            reportTask.GetAwaiter().GetResult();
                            RhinoApp.WriteLine("Status changes reported to server.");
                        }
                    }
                    else
                    {
                        RhinoApp.WriteLine("✓ All files are up to date.");
                    }
                    
                            return Result.Success;
                        }
                        else
                {
                    // Create temporary client to check files
                    var tempClient = new Core.Client.RhinoMCPClient();
                    try
                    {
                        var linkedFiles = tempClient.GetLinkedFiles();
                        
                        if (!linkedFiles.Any())
                        {
                            RhinoApp.WriteLine("No linked files found.");
                            return Result.Success;
                        }
                        
                        RhinoApp.WriteLine($"Found {linkedFiles.Count} linked file(s):");
                        foreach (var file in linkedFiles)
                        {
                            RhinoApp.WriteLine($"  • {Path.GetFileName(file.FilePath)} ({file.Status})");
                        }
                        
                        return Result.Success;
                    }
                    finally
                    {
                        tempClient.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error checking files: {ex.Message}");
                return Result.Failure;
            }
        }

        private Result RunClearFiles(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Clear Linked Files ===");
                RhinoApp.WriteLine("This will clear all linked file records. Active sessions may be affected.");
                
                var confirm = GetUserInput("Are you sure? (yes/no):", "no");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("File clear cancelled.");
                    return Result.Cancel;
                }
                
                var remoteClient = GetRemoteClient(connectionManager);
                if (remoteClient != null)
                {
                    var clearTask = remoteClient.ClearLinkedFilesAsync();
                    clearTask.GetAwaiter().GetResult();
                    RhinoApp.WriteLine("✓ All linked files cleared successfully");
                            return Result.Success;
                        }
                        else
                        {
                    // Create temporary client to clear files
                    var tempClient = new Core.Client.RhinoMCPClient();
                    try
                    {
                        var clearTask = tempClient.ClearLinkedFilesAsync();
                        clearTask.GetAwaiter().GetResult();
                        RhinoApp.WriteLine("✓ All linked files cleared successfully");
                        return Result.Success;
                    }
                    finally
                    {
                        tempClient.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error clearing files: {ex.Message}");
                            return Result.Failure;
                        }
        }

        private Result RunClearLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Clear Stored License ===");
                RhinoApp.WriteLine("This will remove your stored license and you will need to register again.");
                
                var confirm = GetUserInput("Are you sure? (yes/no):", "no");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("License clear cancelled.");
                    return Result.Cancel;
                }
                
                // Clear license using the client
                var remoteClient = GetRemoteClient(connectionManager);
                if (remoteClient != null)
                {
                    remoteClient.ClearStoredLicense();
                    RhinoApp.WriteLine("✓ Stored license cleared successfully");
                    return Result.Success;
                }
                else
                {
                    RhinoApp.WriteLine("✗ Could not access license manager");
                    return Result.Failure;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error clearing license: {ex.Message}");
                return Result.Failure;
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
            
            var result = getter.Get();
            
            if (result == GetResult.String)
            {
                return getter.StringResult();
            }
            
            return null;
        }

        private async Task<bool> RegisterLicenseAsync(string licenseKey, string userId, string serverUrl)
        {
            var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
            var connectionManager = plugin.ConnectionManager;
            
            // Create a temporary remote client to perform registration
            var remoteClient = new Core.Client.RhinoMCPClient();
            try
            {
                return await remoteClient.RegisterLicenseAsync(licenseKey, userId, serverUrl);
            }
            finally
            {
                remoteClient.Dispose();
            }
        }

        private async Task<Core.Client.LicenseValidationResult> CheckLicenseAsync()
        {
            var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
            var connectionManager = plugin.ConnectionManager;
            
            var remoteClient = GetRemoteClient(connectionManager);
            if (remoteClient != null)
            {
                return await remoteClient.GetLicenseStatusAsync();
            }
            catch (Exception ex)
            {
                // Create temporary client to check license
                var tempClient = new Core.Client.RhinoMCPClient();
                try
                {
                    return await tempClient.GetLicenseStatusAsync();
                }
                finally
                {
                    tempClient.Dispose();
                }
            }
        }

        private string GetServerUrlFromLicense()
        {
            try
            {
                var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
                var connectionManager = plugin.ConnectionManager;
                
                var remoteClient = GetRemoteClient(connectionManager);
                if (remoteClient != null)
                {
                    var licenseTask = remoteClient.GetLicenseStatusAsync();
                    var licenseResult = licenseTask.GetAwaiter().GetResult();
                    
                    if (licenseResult.IsValid)
                    {
                        // Note: We need to add ServerUrl to LicenseValidationResult
                        // For now, return null and let user enter it
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Warning: Could not get server URL from license: {ex.Message}");
            }
            
            return null;
        }

        private Core.Client.RhinoMCPClient GetRemoteClient(IConnectionManager connectionManager)
        {
            return connectionManager.ActiveConnection as Core.Client.RhinoMCPClient;
        }

        private enum MainMenuOption
        {
            RegisterLicense,
            CheckLicense,
            StartLocal,
            StartRemote,
            Stop,
            Status,
            CheckFiles,
            ClearFiles,
            ClearLicense,
            Cancel
        }
    }
} 