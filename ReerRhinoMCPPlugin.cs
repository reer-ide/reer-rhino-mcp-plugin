using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Rhino;
using Rhino.PlugIns;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;
using Newtonsoft.Json;

using ReerRhinoMCPPlugin.Commands;
using ReerRhinoMCPPlugin.Core.Client;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using ReerRhinoMCPPlugin.UI;
using ReerRhinoMCPPlugin.UI.Windows;
using System.Security.Cryptography;

namespace ReerRhinoMCPPlugin
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class ReerRhinoMCPPlugin : PlugIn
    {
        private static IConnectionManager connectionManager;
        private static LicenseManager licenseManager;
        private static FileIntegrityManager fileIntegrityManager;
        private static RhinoMCPSettings settings;
        private static bool _avaloniaInitialized = false;
        private ToolExecutor toolExecutor;
        private SaveAsDetector saveAsDetector;

        /// <summary>
        /// Override LoadTime property to auto-load plugin at startup
        /// </summary>
        public override PlugInLoadTime LoadTime
        {
            get { return PlugInLoadTime.AtStartup; }
        }


        public ReerRhinoMCPPlugin()
        {
            Instance = this;

            // Initialize settings
            if (settings == null)
            {
                settings = RhinoMCPSettings.Load(this);
            }

            // Initialize connection manager
            if (connectionManager == null)
                connectionManager = new RhinoMCPConnectionManager();

            // Initialize license manager
            if (licenseManager == null)
                licenseManager = new LicenseManager();

            // Initialize file integrity manager
            if (fileIntegrityManager == null)
            {
                fileIntegrityManager = new FileIntegrityManager();
            }
            
            // Initialize SaveAs detector at plugin level
            if (saveAsDetector == null)
            {
                saveAsDetector = new SaveAsDetector();
                saveAsDetector.SaveAsDetected += OnSaveAsDetected;
            }

            Logger.Success("ReerRhinoMCPPlugin loaded successfully");
        }

        ///<summary>Gets the only instance of the ReerRhinoMCPPlugin plug-in.</summary>
        public static ReerRhinoMCPPlugin Instance { get; private set; }

        /// <summary>
        /// Gets the connection manager for this plugin instance
        /// </summary>
        public RhinoMCPConnectionManager ConnectionManager => connectionManager as RhinoMCPConnectionManager;

        /// <summary>
        /// Gets the license manager for this plugin instance
        /// </summary>
        public LicenseManager LicenseManager => licenseManager;

        /// <summary>
        /// Gets the file integrity manager for this plugin instance
        /// </summary>
        public FileIntegrityManager FileIntegrityManager => fileIntegrityManager;

        /// <summary>
        /// Gets the current plugin settings
        /// </summary>
        public RhinoMCPSettings MCPSettings => settings;

        /// <summary>
        /// Called when the plugin is loaded
        /// </summary>
        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {

                // Initialize Avalonia if not already done
                InitializeAvalonia();

                Logger.Success("ReerRhinoMCPPlugin: Avalonia UI initialized");

                // Load settings
                settings.Load();

                // Initialize tool executor
                toolExecutor = new ToolExecutor();
                ConnectionManager.CommandReceived += OnCommandReceived;

                // Subscribe to connection events
                connectionManager.CommandReceived += OnCommandReceived;
                connectionManager.StatusChanged += OnConnectionStatusChanged;


                // Auto-start if enabled and settings are valid
                if (settings.AutoStart && settings.IsValid())
                {
                    Logger.Info("ReerRhinoMCPPlugin: Auto-starting connection...");

                    // Auto-start on a background task to not block plugin loading
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(2000); // Give Rhino time to finish loading
                            var connectionSettings = settings.GetDefaultConnectionSettings();
                            bool success = await connectionManager.StartConnectionAsync(connectionSettings);

                            if (success)
                            {
                                Logger.Success($"✓ Auto-started {connectionSettings.Mode} connection successfully");
                            }
                            else
                            {
                                Logger.Warning($"⚠ Failed to auto-start {connectionSettings.Mode} connection");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error during auto-start: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Logger.Debug($"Auto-start disabled or invalid settings. AutoStart={settings.AutoStart}, IsValid={settings.IsValid()}");
                }

                return LoadReturnCode.Success;
            }
            catch (System.IO.FileNotFoundException fileEx)
            {
                errorMessage = $"Assembly loading error in REER Rhino MCP Plugin: {fileEx.Message}";
                Logger.Error($"File not found: {fileEx.FileName}");
                Logger.Error($"Full error: {errorMessage}");
                return LoadReturnCode.ErrorShowDialog;
            }
            catch (System.BadImageFormatException imageEx)
            {
                errorMessage = $"Assembly format error in REER Rhino MCP Plugin: {imageEx.Message}";
                Logger.Error($"Bad image format: {errorMessage}");
                return LoadReturnCode.ErrorShowDialog;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load ReerRhinoMCPPlugin: {ex.Message}";
                Logger.Error($"ERROR: {errorMessage}");
                return LoadReturnCode.ErrorShowDialog;
            }
        }

        /// <summary>
        /// Initializes Avalonia UI framework for the plugin
        /// </summary>
        private void InitializeAvalonia()
        {
            Logger.Debug("Enter InitializeAvalonia()");
            if (_avaloniaInitialized)
            {
                Logger.Debug("Avalonia already initialized, skipping.");
                return;
            }
            try
            {
                Logger.Debug("Calling AppBuilder.Configure...");
                AppBuilder.Configure<UI.App>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
                _avaloniaInitialized = true;
                Logger.Debug("Avalonia initialized successfully");
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Setup was already called"))
                {
                    Logger.Debug("Avalonia already initialized, skipping Setup.");
                    _avaloniaInitialized = true;
                }
                else
                {
                    Logger.Error($"Exception in InitializeAvalonia: {ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception in InitializeAvalonia: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Called when the plugin is being shut down
        /// </summary>
        protected override void OnShutdown()
        {
            try
            {
                Logger.Info("Shutting down REER Rhino MCP Plugin...");

                // Control panel cleanup is handled by the new UI system

                // Stop any active connections
                if (connectionManager != null)
                {
                    connectionManager.CommandReceived -= OnCommandReceived;
                    connectionManager.StatusChanged -= OnConnectionStatusChanged;

                    // Stop connection synchronously with timeout
                    var stopTask = connectionManager.StopConnectionAsync();
                    stopTask.Wait(5000); // 5 second timeout

                    connectionManager.Dispose();
                    connectionManager = null;
                }

                // Dispose of SaveAs detector
                if (saveAsDetector != null)
                {
                    saveAsDetector.SaveAsDetected -= OnSaveAsDetected;
                    saveAsDetector.Dispose();
                    saveAsDetector = null;
                }
                
                // Dispose of file integrity manager
                if (fileIntegrityManager != null)
                {
                    fileIntegrityManager.Dispose();
                    fileIntegrityManager = null;
                }

                // Save current settings
                settings?.Save();

                Logger.Success("REER Rhino MCP Plugin shut down successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during plugin shutdown: {ex.Message}");
            }
            finally
            {
                base.OnShutdown();
            }
        }

        /// <summary>
        /// Handles commands received from MCP clients
        /// </summary>
        private void OnCommandReceived(object sender, CommandReceivedEventArgs e)
        {
            try
            {
                var responseJson = toolExecutor.ProcessTool(e.Command, e.ClientId);

                if (ConnectionManager.ActiveConnection != null)
                {
                    ConnectionManager.ActiveConnection.SendResponseAsync(responseJson, e.ClientId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing command in plugin: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles connection status changes
        /// </summary>
        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            // Only log status changes that aren't already logged elsewhere
            if (e.Status == ConnectionStatus.Failed)
            {
                // Failed status with message is already logged by the connection layer
                if (!string.IsNullOrEmpty(e.Message))
                {
                    // Just log the consolidated error message
                    Logger.Info($"Connection failed: {e.Message}");
                }
                else
                {
                    Logger.Info($"MCP Connection: {e.Status}");
                }
            }
            else
            {
                // For non-failure statuses, log normally
                string statusMessage = $"MCP Connection: {e.Status}";
                if (!string.IsNullOrEmpty(e.Message))
                {
                    statusMessage += $" - {e.Message}";
                }
                Logger.Info(statusMessage);
            }

            // TODO: Update status bar if enabled in settings
            if (settings.ShowStatusBar)
            {
                // This will be implemented when we add UI components
            }
        }

        /// <summary>
        /// Handles SaveAs detection events from SaveAsDetector
        /// </summary>
        private async void OnSaveAsDetected(object sender, SaveAsDetector.SaveAsDetectedEventArgs e)
        {
            try
            {
                Logger.Debug("[PLUGIN] OnSaveAsDetected event handler called");
                Logger.Debug($"  Document GUID: {e.DocumentGuid}");
                Logger.Debug($"  Old Path: {e.OldFilePath}");
                Logger.Debug($"  New Path: {e.NewFilePath}");

                // Look up the linked file using the FileIntegrityManager
                var linkedFile = fileIntegrityManager.GetLinkedFileByGUID(e.DocumentGuid);
                
                if (linkedFile == null)
                {
                    Logger.Warning($"No linked session found for document GUID {e.DocumentGuid} during SaveAs");
                    Logger.Info("This might happen if:");
                    Logger.Info("  1. This document was never linked to Reer before");
                    Logger.Info("  2. Document GUID was not properly registered");
                    Logger.Info("  3. Session was cleaned up or expired");
                    Logger.Info("Skipping SaveAs handling.");
                    return;
                }
                
                // Update the event args with the linked file info
                e.LinkedFileInfo = linkedFile;
                
                Logger.Success($"Found linked session {linkedFile.SessionId} for SaveAs operation");
                Logger.Info($"SaveAs detected for session {linkedFile.SessionId}: '{e.OldFilePath}' -> '{e.NewFilePath}'");

                // Show user confirmation dialog
                Logger.Debug("Showing SaveAs confirmation dialog...");
                var userChoice = await SaveAsDetector.ShowSaveAsConfirmationDialog(e.OldFilePath, e.NewFilePath);
                Logger.Debug($"User choice: {userChoice}");

                switch (userChoice)
                {
                    case SaveAsUserChoice.ContinueWithNewFile:
                        Logger.Info("User chose to continue with new file");
                        
                        // Instead of just updating the path, we need to restart the connection
                        // This ensures we don't accidentally update multiple sessions
                        
                        // First, update the local file path in storage
                        await fileIntegrityManager.UpdateSessionFilePathAsync(linkedFile.SessionId, e.NewFilePath);
                        
                        // Check if we have an active remote connection
                        if (connectionManager.ActiveConnection.Settings.Mode == ConnectionMode.Remote && connectionManager.IsConnected)
                        {
                            Logger.Info("Restarting remote connection with updated file path...");

                            // Stop the current connection
                            await connectionManager.StopConnectionAsync(false);

                            // Create a FilePathChanged validation to trigger proper reconnection
                            var validation = new FileConnectionValidation
                            {
                                FilePath = e.NewFilePath,
                                DocumentGUID = e.DocumentGuid,
                                SessionId = linkedFile.SessionId,
                                LinkedFileInfo = linkedFile,
                                ValidationScenario = FileValidationScenario.FilePathChanged,
                                IsValid = false,
                                RequiresUserDecision = false // Already decided by user
                            };
                            
                            // Restart connection with the validation
                            var settings = new ConnectionSettings
                            {
                                Mode = ConnectionMode.Remote,
                                RemoteUrl = ConnectionSettings.GetServerUrl()
                            };
                            
                            var success = await connectionManager.StartConnectionAsync(settings, validation);
                            if (success)
                            {
                                Logger.Success("Remote connection restarted with new file path");
                            }
                            else
                            {
                                Logger.Warning("Failed to restart remote connection");
                            }
                        }
                        else
                        {
                            Logger.Info("No active remote connection to update");
                        }
                        break;

                    case SaveAsUserChoice.ReturnToOriginalFile:
                        Logger.Info("User chose to return to original file");
                        // Open the original file to maintain session continuity
                        var openFileSuccess = await OpenFileInRhino(e.OldFilePath);
                        if (openFileSuccess)
                        {
                            Logger.Success($"Returned to original file: {System.IO.Path.GetFileName(e.OldFilePath)}");
                        }
                        else
                        {
                            Logger.Warning($"Could not open original file: {e.OldFilePath}");
                        }
                        break;

                    case SaveAsUserChoice.Cancel:
                        Logger.Info("User cancelled SaveAs handling");
                        break;
                }
                
                Logger.Debug("[PLUGIN] OnSaveAsDetected event handler completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling SaveAs detection: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

#region Utility Methods
        /// <summary>
        /// Open a file in Rhino
        /// </summary>
        private Task<bool> OpenFileInRhino(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    Logger.Error($"File does not exist: {filePath}");
                    return Task.FromResult(false);
                }

                // Use Rhino's command to open the file
                bool openSuccess = Rhino.RhinoApp.RunScript($"-_Open \"{filePath}\"", true);
                return Task.FromResult(openSuccess);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error opening file in Rhino: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        // SaveAsUserChoice enum moved to SaveAsDetector class

        /// <summary>
        /// Shows the new refactored control panel (for testing)
        /// </summary>
        public void ShowNewControlPanel()
        {
            try
            {
                Logger.Debug("ShowNewControlPanel called");
                if (!_avaloniaInitialized)
                {
                    Logger.Debug("Avalonia not initialized, initializing now...");
                    InitializeAvalonia();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var newPanel = new UI.Windows.MCPControlPanelNew(this);
                        newPanel.Show();
                        Logger.Success("New control panel shown successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to show new control panel: {ex.Message}");
                        Logger.Error($"Stack trace: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ShowNewControlPanel: {ex.Message}");
            }
        }
#endregion
        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}