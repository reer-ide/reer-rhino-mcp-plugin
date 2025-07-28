using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Rhino;
using Rhino.PlugIns;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;

using ReerRhinoMCPPlugin.Commands;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using ReerRhinoMCPPlugin.UI;
using ReerRhinoMCPPlugin.UI.Windows;

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
        private static RhinoMCPSettings settings;
        private static bool _avaloniaInitialized = false;
        private ToolExecutor toolExecutor;

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
            
            Logger.Success("ReerRhinoMCPPlugin loaded successfully");
        }

        ///<summary>Gets the only instance of the ReerRhinoMCPPlugin plug-in.</summary>
        public static ReerRhinoMCPPlugin Instance { get; private set; }
        
        /// <summary>
        /// Gets the connection manager for this plugin instance
        /// </summary>
        public RhinoMCPConnectionManager ConnectionManager => connectionManager as RhinoMCPConnectionManager;
        
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
            string statusMessage = $"MCP Connection: {e.Status}";
            
            if (!string.IsNullOrEmpty(e.Message))
            {
                statusMessage += $" - {e.Message}";
            }
            
            Logger.Info(statusMessage);
            
            // TODO: Update status bar if enabled in settings
            if (settings.ShowStatusBar)
            {
                // This will be implemented when we add UI components
            }
        }

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
                        var newPanel = new UI.Windows.MCPControlPanel(this);
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

        public void ShowControlPanel()
        {
            try
            {
                Logger.Debug("ShowControlPanel called - using new UI");
                // Redirect to new UI implementation
                ShowNewControlPanel();

            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing control panel: {ex}");
            }
        }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}