using System;
using Rhino;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;
using ReerRhinoMCPPlugin.Functions;

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
    public class ReerRhinoMCPPlugin : Rhino.PlugIns.PlugIn
    {
        private IConnectionManager connectionManager;
        private RhinoMCPSettings settings;
        private MCPCommandRouter mcpCommandRouter;
        
        public ReerRhinoMCPPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the ReerRhinoMCPPlugin plug-in.</summary>
        public static ReerRhinoMCPPlugin Instance { get; private set; }
        
        /// <summary>
        /// Gets the connection manager for this plugin instance
        /// </summary>
        public IConnectionManager ConnectionManager => connectionManager;
        
        /// <summary>
        /// Gets the current plugin settings
        /// </summary>
        public RhinoMCPSettings MCPSettings => settings;

        /// <summary>
        /// Called when the plugin is loaded
        /// </summary>
        protected override Rhino.PlugIns.LoadReturnCode OnLoad(ref string errorMessage)
        {
            try
            {
                RhinoApp.WriteLine("Loading REER Rhino MCP Plugin...");
                
                // Load settings - pass this instance to avoid null reference
                settings = RhinoMCPSettings.Load(this);

                // Initialize command handler
                mcpCommandRouter = new MCPCommandRouter();
                
                // Initialize connection manager
                connectionManager = new RhinoMCPConnectionManager();
                
                // Subscribe to connection events
                connectionManager.CommandReceived += OnCommandReceived;
                connectionManager.StatusChanged += OnConnectionStatusChanged;
                
                RhinoApp.WriteLine("REER Rhino MCP Plugin loaded successfully");
                
                // Auto-start connection if enabled
                if (settings.AutoStart && settings.IsValid())
                {
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var connectionSettings = settings.GetDefaultConnectionSettings();
                            bool success = await connectionManager.StartConnectionAsync(connectionSettings);
                            
                            if (success)
                            {
                                RhinoApp.WriteLine("Auto-started MCP connection");
                            }
                            else
                            {
                                RhinoApp.WriteLine("Failed to auto-start MCP connection");
                            }
                        }
                        catch (Exception ex)
                        {
                            RhinoApp.WriteLine($"Error during auto-start: {ex.Message}");
                        }
                    });
                }
                
                return Rhino.PlugIns.LoadReturnCode.Success;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error loading REER Rhino MCP Plugin: {ex.Message}";
                RhinoApp.WriteLine(errorMessage);
                return Rhino.PlugIns.LoadReturnCode.ErrorShowDialog;
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

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}