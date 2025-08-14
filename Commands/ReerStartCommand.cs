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
        private enum StartMenuOption { Local, Remote, Cancel }
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

        public async Task<bool> RunStartLocalAsync(IConnectionManager connectionManager, string host, int port)
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

        public async Task<bool> RunStartRemoteAsync(IConnectionManager connectionManager)
        {
            try
            {
                // Pre-validate file before attempting connection
                var filePath = GetCurrentRhinoFilePath();
                var existingDocumentGuid = FileIntegrityManager.GetExistingDocumentGUID();
                
                if (!string.IsNullOrEmpty(existingDocumentGuid))
                {
                    Logger.Info($"Found existing document GUID: {existingDocumentGuid}");
                }
                
                // Perform file validation (server will generate GUID for new files)
                var validation = await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.ValidateFileForConnectionAsync(filePath, existingDocumentGuid);
                
                // Handle validation errors first
                if (validation.ValidationError)
                {
                    RhinoApp.WriteLine($"File validation error: {validation.Message}");
                    return false;
                }
                
                // Handle validation scenarios that require user input
                bool shouldProceedWithConnection = await HandleValidationScenarioAsync(validation, filePath, existingDocumentGuid);
                if (!shouldProceedWithConnection)
                {
                    return false;
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
                var success = await connectionManager.StartConnectionAsync(settings, validation);

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
                    // Error already logged by connection manager - just show user-friendly message
                    RhinoApp.WriteLine("✗ Failed to establish remote connection.");
                    RhinoApp.WriteLine("Check the console for details.");
                }
                return success;
            }
            catch (Exception ex)
            {
                // Log only if it's not a "No session found" error (already logged)
                if (!ex.Message.StartsWith("No session found"))
                {
                    Logger.Error($"Error starting remote connection: {ex.Message}");
                }
                else
                {
                    // For session not found, provide helpful guidance
                    RhinoApp.WriteLine("✗ No session found for this file.");
                    RhinoApp.WriteLine("Please ensure the host application has created a session first.");
                }
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

        /// <summary>
        /// Handle validation scenarios that require user input
        /// </summary>
        /// <param name="validation">Validation result</param>
        /// <param name="filePath">Current file path</param>
        /// <param name="existingDocumentGuid">Existing document GUID</param>
        /// <returns>True if connection should proceed, false otherwise</returns>
        private async Task<bool> HandleValidationScenarioAsync(FileConnectionValidation validation, string filePath, string existingDocumentGuid)
        {
            switch (validation.ValidationScenario)
            {
                case FileValidationScenario.PerfectMatch:
                case FileValidationScenario.NoLinkFound:
                    // These scenarios don't require user input - let the connection logic handle them
                    return true;
                    
                case FileValidationScenario.FilePathChanged:
                    return await HandleFilePathChangedAsync(validation, filePath);
                    
                case FileValidationScenario.FileReplaced:
                    return await HandleFileReplacedAsync(validation, filePath, existingDocumentGuid);
                    
                default:
                    Logger.Warning($"Unknown validation scenario: {validation.ValidationScenario}");
                    return false;
            }
        }
        
        /// <summary>
        /// Handle file path changed scenario
        /// </summary>
        private async Task<bool> HandleFilePathChangedAsync(FileConnectionValidation validation, string filePath)
        {
            Logger.Warning("═══════════════════════════════════════════════════");
            Logger.Warning("FILE PATH CHANGED");
            Logger.Warning($"Original file path: {validation.LinkedFileInfo?.FilePath}");
            Logger.Warning($"Current file path: {filePath}");
            Logger.Warning("This usually happens when the file was moved or renamed.");
            Logger.Warning("═══════════════════════════════════════════════════");
            
            var tcs = new TaskCompletionSource<bool>();
            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var result = Rhino.UI.Dialogs.ShowMessage(
                        $"The file path has changed. " +
                        "Do you want to continue using the existing session with this file?\n" +
                        "YES - Use this file with the existing session\n" +
                        "NO - Please re-link the file through the host application",
                        "File Path Change Detected",
                        Rhino.UI.ShowMessageButton.YesNo,
                        Rhino.UI.ShowMessageIcon.Question);
                    tcs.SetResult(result == Rhino.UI.ShowMessageResult.Yes);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error showing confirmation dialog: {ex.Message}");
                    tcs.SetResult(false);
                }
            }));

            bool shouldContinue = await tcs.Task;
            
            if (!shouldContinue)
            {
                Logger.Info("User chose to create new session");
                Logger.Info("Please link this file through the host application");
                // Only remove the current document's GUID, don't unregister the linked file
                // (which would affect the file that's actually using that session)
                FileIntegrityManager.DeleteDocumentGUID();
            }
            
            return shouldContinue;
        }

        /// <summary>
        /// Handle file replaced scenario
        /// </summary>
        private async Task<bool> HandleFileReplacedAsync(FileConnectionValidation validation, string filePath, string existingDocumentGuid)
        {
            Logger.Info("═══════════════════════════════════════════════════");
            Logger.Info("FILE REPLACED - AUTO-CONNECTING");
            Logger.Info($"File at: {filePath}");
            Logger.Info($"Original session GUID: {validation.LinkedFileInfo?.DocumentGUID}");
            Logger.Info($"Current file GUID: {existingDocumentGuid ?? "none"}");
            Logger.Info("═══════════════════════════════════════════════════");
            Logger.Info("Automatically continuing with existing session...");
            
            // Since the user replaced the file at the same path, they likely want to continue
            // with the existing session. Set the document GUID to match the session.
            FileIntegrityManager.SetDocumentGUID(validation.LinkedFileInfo.DocumentGUID);
            Logger.Info($"Document GUID updated to match session: {validation.LinkedFileInfo.DocumentGUID}");
            
            // Always return true to continue with the connection
            return await Task.FromResult(true);
        }
    }
}
