using System;
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
        /// Will stop any existing connection first
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        public async Task<bool> StartConnectionAsync(ConnectionSettings settings)
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
                // Stop any existing connection first
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
                bool success = await newConnection.StartAsync(settings);
                
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