using System;
using System.IO;
using System.Reflection;
using Rhino;
using Rhino.PlugIns;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;
using ReerRhinoMCPPlugin.Functions;
using ReerRhinoMCPPlugin.Commands;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReerRhinoMCPPlugin.UI;

namespace rhino_mcp_plugin
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
        private static MCPCommandRouter mcpCommandRouter;
        private static bool _avaloniaInitialized = false;
        
        public ReerRhinoMCPPlugin()
        {
            Instance = this;
            
            // Initialize settings
            if (settings == null)
                settings = new RhinoMCPSettings();
            
            // Initialize connection manager
            if (connectionManager == null)
                connectionManager = new RhinoMCPConnectionManager();
            
            RhinoApp.WriteLine("ReerRhinoMCPPlugin loaded successfully");
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
                
                RhinoApp.WriteLine("ReerRhinoMCPPlugin: Avalonia UI initialized");
                
                // Load settings
                settings.Load();
                
                // Initialize command router
                mcpCommandRouter = new MCPCommandRouter();
                
                // Subscribe to connection events
                connectionManager.CommandReceived += OnCommandReceived;
                connectionManager.StatusChanged += OnConnectionStatusChanged;
                
                // Auto-start if enabled
                if (settings.AutoStart)
                {
                    RhinoApp.WriteLine("ReerRhinoMCPPlugin: Auto-starting server...");
                    // Note: In a real implementation, you might want to delay this
                    // until Rhino is fully loaded
                }
                
                return LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to load ReerRhinoMCPPlugin: {ex.Message}";
                RhinoApp.WriteLine($"ERROR: {errorMessage}");
                return LoadReturnCode.ErrorShowDialog;
            }
        }
        
        /// <summary>
        /// Initializes Avalonia UI framework for the plugin
        /// </summary>
        private void InitializeAvalonia()
        {
            RhinoApp.WriteLine("[DEBUG] Enter InitializeAvalonia()");
            if (_avaloniaInitialized)
            {
                RhinoApp.WriteLine("[DEBUG] Avalonia already initialized, skipping.");
                return;
            }
            try
            {
                RhinoApp.WriteLine("[DEBUG] Calling AppBuilder.Configure...");
                AppBuilder.Configure<App>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
                _avaloniaInitialized = true;
                RhinoApp.WriteLine("[DEBUG] Avalonia initialized successfully");
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("Setup was already called"))
                {
                    RhinoApp.WriteLine("[DEBUG] Avalonia already initialized, skipping Setup.");
                    _avaloniaInitialized = true;
                }
                else
                {
                    RhinoApp.WriteLine($"[ERROR] Exception in InitializeAvalonia: {ex}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Exception in InitializeAvalonia: {ex}");
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
                RhinoApp.WriteLine("Shutting down REER Rhino MCP Plugin...");
                
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
                
                RhinoApp.WriteLine("REER Rhino MCP Plugin shut down successfully");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during plugin shutdown: {ex.Message}");
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
                if (settings.EnableDebugLogging)
                {
                    RhinoApp.WriteLine($"Received MCP command from {e.ClientId}: {e.Command["type"]}");
                }
                
                // Process command using the command handler
                string response = mcpCommandRouter.ProcessCommand(e.Command, e.ClientId);
                
                // Send response back to client
                _ = connectionManager.ActiveConnection?.SendResponseAsync(response, e.ClientId);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing command: {ex.Message}");
                
                // Send error response
                string errorResponse = "{\"status\":\"error\",\"message\":\"Internal server error\"}";
                _ = connectionManager.ActiveConnection?.SendResponseAsync(errorResponse, e.ClientId);
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
            
            RhinoApp.WriteLine(statusMessage);
            
            // TODO: Update status bar if enabled in settings
            if (settings.ShowStatusBar)
            {
                // This will be implemented when we add UI components
            }
        }

        public void ShowControlPanel()
        {
            try
            {
                RhinoApp.WriteLine("[DEBUG] ShowControlPanel called");
                if (!_avaloniaInitialized)
                {
                    RhinoApp.WriteLine("[DEBUG] Avalonia not initialized, initializing now...");
                    InitializeAvalonia();
                }

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        RhinoApp.WriteLine("[DEBUG] Creating MCPControlPanel window...");
                        var controlPanel = new MCPControlPanel(this);
                        RhinoApp.WriteLine("[DEBUG] MCPControlPanel instance created");
                        controlPanel.Show();
                        RhinoApp.WriteLine("[DEBUG] MCPControlPanel.Show() called");
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"[ERROR] Exception in Dispatcher.UIThread.Post: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Error showing control panel: {ex}");
            }
        }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}