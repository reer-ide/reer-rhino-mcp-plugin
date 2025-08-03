using System;
using System.IO;
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
            RhinoApp.WriteLine("Note: The host application must create a session for this file first.");
            
            // Check if file has been saved
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null || string.IsNullOrEmpty(doc.Path))
            {
                RhinoApp.WriteLine("Warning: Current document is not saved.");
                var savePrompt = GetUserInput("Save the document before connecting? (yes/no):", "yes");
                if (savePrompt?.ToLower() == "yes")
                {
                    RhinoApp.RunScript("_Save", false);
                    // Re-check if saved
                    if (string.IsNullOrEmpty(RhinoDoc.ActiveDoc?.Path))
                    {
                        RhinoApp.WriteLine("Document not saved. Connection cancelled.");
                        return Result.Cancel;
                    }
                }
            }
            
            RhinoApp.WriteLine("Checking file integrity and connecting to existing session...");
            
            // Run async operation in background
            Task.Run(async () => await RunStartRemoteAsync(connectionManager));
            return Result.Success;
        }

        private async Task<bool> RunStartRemoteAsync(IConnectionManager connectionManager)
        {
            try
            {
                // Pre-validate file before attempting connection
                var filePath = GetCurrentRhinoFilePath();
                var documentGuid = DocumentGUIDHelper.GetOrCreateDocumentGUID();
                
                if (!string.IsNullOrEmpty(documentGuid))
                {
                    Logger.Info($"Document GUID: {documentGuid}");
                }
                
                // Perform file validation
                var validation = await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.ValidateFileForConnectionAsync(filePath, documentGuid);
                
                // Handle validation scenarios that require user input
                if (validation.RequiresUserDecision)
                {
                    switch (validation.ValidationScenario)
                    {
                        case FileValidationScenario.FileReplacedNoGUID:
                            Logger.Warning("═══════════════════════════════════════════════════");
                            Logger.Warning("FILE CHANGE DETECTED");
                            Logger.Warning($"File at: {filePath}");
                            Logger.Warning($"This file has no document GUID, but a previous file with GUID was linked at this path.");
                            Logger.Warning($"Previous file GUID: {validation.LinkedFileInfo?.DocumentGUID}");
                            Logger.Warning("═══════════════════════════════════════════════════");
                            
                            // Get user confirmation on main thread (thread-safe)
                            var tcs = new TaskCompletionSource<bool>();
                            RhinoApp.InvokeOnUiThread(new Action(() => 
                            {
                                try
                                {
                                    var result = Rhino.UI.Dialogs.ShowMessage(
                                        $"The file '{Path.GetFileName(filePath)}' appears to have been replaced.\n\n" +
                                        "Do you want to continue using the existing session with this file?\n\n" +
                                        "YES - Use this file with the existing session\n" +
                                        "NO - Create a new session through the host application",
                                        "File Replacement Detected",
                                        Rhino.UI.ShowMessageButton.YesNo,
                                        Rhino.UI.ShowMessageIcon.Question);
                                    tcs.SetResult(result == Rhino.UI.ShowMessageResult.Yes);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"Error showing confirmation dialog: {ex.Message}");
                                    tcs.SetResult(false); // Default to safe option
                                }
                            }));
                            
                            bool shouldContinue = await tcs.Task;
                            
                            if (shouldContinue)
                            {
                                Logger.Info("User confirmed to continue with existing session");
                                // Update the linked file with new GUID
                                var newGuid = DocumentGUIDHelper.GetOrCreateDocumentGUID();
                                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.RegisterLinkedFileAsync(
                                    validation.LinkedFileInfo.SessionId, filePath, newGuid);
                                Logger.Info($"Updated file with new GUID: {newGuid}");
                                // Continue with connection
                            }
                            else
                            {
                                Logger.Info("User chose to create new session");
                                Logger.Info("Please link this file through the host application");
                                // Clean up the old linked file info
                                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.LinkedFileInfo.SessionId);
                                return false;
                            }
                            break;
                            
                        case FileValidationScenario.FileReplaced:
                            Logger.Warning("═══════════════════════════════════════════════════");
                            Logger.Warning("FILE REPLACEMENT DETECTED");
                            Logger.Warning($"A different file exists at: {filePath}");
                            Logger.Warning($"Original session was linked to a file with GUID: {validation.LinkedFileInfo?.DocumentGUID}");
                            Logger.Warning($"Current file has GUID: {documentGuid}");
                            Logger.Warning("═══════════════════════════════════════════════════");
                            Logger.Warning("This usually happens when:");
                            Logger.Warning("- The original file was deleted and replaced with a new one");
                            Logger.Warning("- You opened a different file with the same name");
                            Logger.Warning("");
                            Logger.Warning("You need to create a new session through the host application.");
                            
                            // Clean up the old linked file info
                            if (validation.LinkedFileInfo != null)
                            {
                                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.UnregisterLinkedFileAsync(validation.LinkedFileInfo.SessionId);
                            }
                            return false;
                    }
                }
                
                // Use server URL based on development mode setting
                var serverUrl = ConnectionSettings.GetServerUrl();
                var mode = ReerRhinoMCPPlugin.Instance.MCPSettings.DevelopmentMode ? "development" : "production";
                Logger.Info($"Using {mode} server: {serverUrl}");

                var settings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Remote,
                    RemoteUrl = serverUrl
                };

                Logger.Info($"Starting remote connection to: {serverUrl}");
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
                    Logger.Error("Possible reasons:");
                    Logger.Error("- No session exists for this file (host app must create session first)");
                    Logger.Error("- Session has expired or been deactivated");
                    Logger.Error("- File has been modified since session creation");
                    Logger.Error("- License validation failed");
                }
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting remote connection: {ex.Message}");
                return false;
            }
        }
        
        private string GetCurrentRhinoFilePath()
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc != null && !string.IsNullOrEmpty(doc.Path))
                {
                    return doc.Path;
                }
                else
                {
                    // Return a placeholder for unsaved documents
                    return $"/rhino/unsaved_document_{DateTime.Now:yyyyMMdd_HHmmss}.3dm";
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Warning: Could not get current file path: {ex.Message}");
                return $"/rhino/unknown_document_{DateTime.Now:yyyyMMdd_HHmmss}.3dm";
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
