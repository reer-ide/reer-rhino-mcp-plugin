using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Core.Server;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Core
{
    /// <summary>
    /// Manages MCP connections and ensures only one is active at a time
    /// </summary>
    public class RhinoMCPConnectionManager : IConnectionManager
    {
        private readonly object lockObject = new object();
        private IRhinoMCPConnection activeConnection;
        private bool disposed;
        
        /// <summary>
        /// The currently active connection (null if none)
        /// </summary>
        public IRhinoMCPConnection ActiveConnection
        {
            get
            {
                lock (lockObject)
                {
                    return activeConnection;
                }
            }
        }
        
        /// <summary>
        /// Indicates whether any connection is currently active
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (lockObject)
                {
                    return activeConnection?.IsConnected ?? false;
                }
            }
        }
        
        /// <summary>
        /// The current connection status
        /// </summary>
        public ConnectionStatus Status
        {
            get
            {
                lock (lockObject)
                {
                    return activeConnection?.Status ?? ConnectionStatus.Disconnected;
                }
            }
        }
        
        /// <summary>
        /// Number of connected clients (only applicable for server mode)
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (lockObject)
                {
                    if (activeConnection is RhinoMCPServer server)
                    {
                        return server.ClientCount;
                    }
                    return 0;
                }
            }
        }
        
        /// <summary>
        /// Event fired when a command is received from any active connection
        /// </summary>
        public event EventHandler<CommandReceivedEventArgs> CommandReceived;
        
        /// <summary>
        /// Event fired when the connection status changes
        /// </summary>
        public event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
        
        /// <summary>
        /// Starts a connection with the specified settings
        /// Will reuse existing connection if it's compatible with the requested settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <param name="fileValidation">Optional pre-computed file validation result (for remote connections)</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public async Task<bool> StartConnectionAsync(ConnectionSettings settings, FileConnectionValidation fileValidation = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            if (!settings.IsValid())
            {
                Logger.Error("Invalid connection settings provided");
                return false;
            }

            try
            {
                // Check if current connection is already compatible
                lock (lockObject)
                {
                    if (activeConnection != null && activeConnection.IsConnected && 
                        IsConnectedToSameFile(activeConnection, settings))
                    {
                        Logger.Info($"Reusing existing compatible {settings.Mode} connection");
                        return true;
                    }
                }

                // Stop existing connection if it's not compatible
                await StopConnectionAsync();
                
                // Create new connection based on mode
                IRhinoMCPConnection newConnection = CreateConnection(settings.Mode);
                
                if (newConnection == null)
                {
                    Logger.Error($"Failed to create connection for mode: {settings.Mode}");
                    return false;
                }
                
                // Subscribe to events before starting
                newConnection.CommandReceived += OnConnectionCommandReceived;
                newConnection.StatusChanged += OnConnectionStatusChanged;
                
                // Start the connection
                bool success = await newConnection.StartAsync(settings, fileValidation);
                
                if (success)
                {
                    lock (lockObject)
                    {
                        activeConnection = newConnection;
                    }
                    
                    Logger.Success($"RhinoMCP connection started successfully in {settings.Mode} mode");
                    return true;
                }
                else
                {
                    // Clean up on failure
                    newConnection.CommandReceived -= OnConnectionCommandReceived;
                    newConnection.StatusChanged -= OnConnectionStatusChanged;
                    newConnection.Dispose();
                    
                    Logger.Error($"Failed to start RhinoMCP connection in {settings.Mode} mode");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error starting connection: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Stops the current connection if one is active
        /// </summary>
        /// <param name="cleanSessionInfo">Whether to clean stored session info (false for remote connections to maintain persistence)</param>
        /// <returns>Task representing the async operation</returns>
        public async Task StopConnectionAsync(bool cleanSessionInfo = true)
        {
            IRhinoMCPConnection connectionToStop = null;
            lock (lockObject)
            {
                if (activeConnection != null)
                {
                    connectionToStop = activeConnection;
                    activeConnection = null;
                }
            }
            
            if (connectionToStop != null)
            {
                try
                {
                    Logger.Info("Stopping RhinoMCP connection...");
                    
                    // Unsubscribe from events first
                    connectionToStop.CommandReceived -= OnConnectionCommandReceived;
                    connectionToStop.StatusChanged -= OnConnectionStatusChanged;
                    
                    // Stop the connection with timeout
                    using (var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        try
                        {
                            await connectionToStop.StopAsync(cleanSessionInfo);
                            Logger.Success("Connection stopped successfully");
                        }
                        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                        {
                            Logger.Error("Stop operation timed out, forcing disposal");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error during graceful stop: {ex.Message}");
                        }
                    }
                    
                    // Always dispose resources
                    try
                    {
                        connectionToStop.Dispose();
                        Logger.Info("Connection resources disposed");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error disposing connection: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error stopping connection: {ex.Message}");
                }
            }
            else
            {
                Logger.Info("No active connection to stop");
            }
        }
        
        /// <summary>
        /// Switches to a different connection mode
        /// Will stop current connection and start new one
        /// </summary>
        /// <param name="settings">New connection settings</param>
        /// <returns>True if switch was successful, false otherwise</returns>
        public async Task<bool> SwitchConnectionAsync(ConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            
            Logger.Info($"Switching to {settings.Mode} mode...");
            return await StartConnectionAsync(settings);
        }
        
        /// <summary>
        /// Creates a connection instance based on the specified mode
        /// </summary>
        /// <param name="mode">Connection mode</param>
        /// <returns>Connection instance or null if mode is not supported</returns>
        private IRhinoMCPConnection CreateConnection(ConnectionMode mode)
        {
            switch (mode)
            {
                case ConnectionMode.Local:
                    return new RhinoMCPServer();
                    
                case ConnectionMode.Remote:
                    return new RhinoMCPClient();
                    
                default:
                    Logger.Error($"Unsupported connection mode: {mode}");
                    return null;
            }
        }

        /// <summary>
        /// Check if the existing connection is established for the same file
        /// For remote connections, also validates the server-side session status
        /// </summary>
        /// <param name="connection">Current active connection</param>
        /// <param name="settings">Requested connection settings</param>
        /// <returns>True if compatible, false otherwise</returns>
        private bool IsConnectedToSameFile(IRhinoMCPConnection connection, ConnectionSettings settings)
        {
            if (connection?.Settings == null)
                return false;

            var currentSettings = connection.Settings;

            // Mode must match
            if (currentSettings.Mode != settings.Mode)
            {
                Logger.Info($"Current connection mode {currentSettings.Mode} does not match requested mode {settings.Mode}");
                return false;
            }
            else
            {
                switch (settings.Mode)
                {
                    case ConnectionMode.Local:
                        // For local connections, host and port must match
                        return string.Equals(currentSettings.LocalHost, settings.LocalHost, StringComparison.OrdinalIgnoreCase) &&
                               currentSettings.LocalPort == settings.LocalPort;

                    case ConnectionMode.Remote:
                        // For remote connections, URL must match and session should be valid
                        if (!string.Equals(currentSettings.RemoteUrl, settings.RemoteUrl, StringComparison.OrdinalIgnoreCase))
                            return false;

                        // For remote connections, we need to check if current session can handle the current file
                        // Use ConfigureAwait(false) to prevent deadlocks on any platform
                        return IsRemoteConnectionCompatibleAsync((RhinoMCPClient)connection).ConfigureAwait(false).GetAwaiter().GetResult();

                    default:
                        return false;
                }
            }
        }

        
        /// <summary>
        /// Check if the remote connection is compatible with the current Rhino file
        /// </summary>
        /// <param name="client">Remote MCP client</param>
        /// <returns>True if the connection can be reused, false otherwise</returns>
        private async Task<bool> IsRemoteConnectionCompatibleAsync(RhinoMCPClient client)
        {
            try
            {
                // Check if current session is still valid on server
                if (!await client.IsCurrentSessionValidAsync())
                {
                    Logger.Info("Current session is no longer valid on server");
                    return false;
                }
                
                // Get current file path in Rhino
                var currentFilePath = GetCurrentRhinoFilePath();
                var clientFilePath = client.CurrentFilePath;
                
                // If file paths match, we can definitely reuse the connection
                if (string.Equals(currentFilePath, clientFilePath, StringComparison.Ordinal))
                {
                    Logger.Info("File path matches existing session - reusing connection");
                    return true;
                }
                
                // Different file - check if we can connect to a session for the new file
                // This is a quick check to see if a session exists for the current file
                return await CanConnectToSessionForFileAsync(client, currentFilePath);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking remote connection compatibility: {ex.Message}");
                return false; // Default to creating new connection if check fails
            }
        }
        
        /// <summary>
        /// Check if a session exists for the given file that the client can connect to
        /// </summary>
        /// <param name="client">Remote MCP client</param>
        /// <param name="filePath">File path to check</param>
        /// <returns>True if a compatible session exists, false otherwise</returns>
        private Task<bool> CanConnectToSessionForFileAsync(RhinoMCPClient client, string filePath)
        {
            try
            {
                // This is a lightweight check - we look at local cache first
                var linkedFiles = ReerRhinoMCPPlugin.Instance.FileIntegrityManager.GetAllLinkedFiles();
                var matchingFile = linkedFiles.FirstOrDefault(f => 
                    string.Equals(f.FilePath, filePath, StringComparison.Ordinal) &&
                    f.Status == FileStatus.Available);
                
                if (matchingFile != null)
                {
                    Logger.Info($"Found cached session for file: {System.IO.Path.GetFileName(filePath)}");
                    return Task.FromResult(true);
                }
                
                // If no cached session, we'll need a new connection to attempt connecting
                // Return false to trigger new connection process
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error checking session availability for file: {ex.Message}");
                return Task.FromResult(false);
            }
        }
        
        /// <summary>
        /// Get the current Rhino file path
        /// </summary>
        /// <returns>Current file path or placeholder for unsaved documents</returns>
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
        
        /// <summary>
        /// Handles command received events from the active connection
        /// </summary>
        private void OnConnectionCommandReceived(object sender, CommandReceivedEventArgs e)
        {
            CommandReceived?.Invoke(this, e);
        }
        
        /// <summary>
        /// Handles status change events from the active connection
        /// </summary>
        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Notify server about file path change (for SaveAs operations)
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="oldPath">Previous file path</param>
        /// <param name="newPath">New file path</param>
        /// <param name="documentGuid">Document GUID for verification</param>
        /// <returns>True if notification was successful</returns>
        public async Task<bool> NotifyServerOfFilePathChangeAsync(string sessionId, string oldPath, string newPath, string documentGuid)
        {
            try
            {
                lock (lockObject)
                {
                    if (activeConnection == null)
                    {
                        Logger.Debug("No active connection for server notification");
                        return false;
                    }
                }

                // Only remote connections need server notification
                var remoteConnection = activeConnection as RhinoMCPClient;
                if (remoteConnection == null)
                {
                    Logger.Debug("Active connection is not remote, skipping server notification");
                    return false;
                }

                // Use the remote client to notify server
                var success = await remoteConnection.NotifyServerOfFilePathChangeAsync(sessionId, oldPath, newPath, documentGuid);
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error notifying server of file path change: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Disposes the connection manager and any active connections
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Task.Run(async () => await StopConnectionAsync()).Wait(5000);
                disposed = true;
            }
        }
    }
} 