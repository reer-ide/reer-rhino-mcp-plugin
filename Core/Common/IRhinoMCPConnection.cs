using System;
using System.Threading.Tasks;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Common interface for both local TCP server and remote WebSocket client connections
    /// </summary>
    public interface IRhinoMCPConnection : IDisposable
    {
        /// <summary>
        /// Indicates whether the connection is currently active
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// The current connection status
        /// </summary>
        ConnectionStatus Status { get; }
        
        /// <summary>
        /// The connection settings being used
        /// </summary>
        ConnectionSettings Settings { get; }
        
        /// <summary>
        /// Starts the connection with the specified settings
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        Task<bool> StartAsync(ConnectionSettings settings);
        
        /// <summary>
        /// Stops the connection and cleans up resources
        /// </summary>
        /// <param name="cleanSessionInfo">Whether to clean stored session info (default: true)</param>
        /// <returns>Task representing the async operation</returns>
        Task StopAsync(bool cleanSessionInfo = true);
        
        /// <summary>
        /// Sends a response back to the client
        /// </summary>
        /// <param name="responseJson">JSON response to send</param>
        /// <param name="clientId">ID of the client to send to (for multi-client scenarios)</param>
        /// <returns>True if response was sent successfully, false otherwise</returns>
        Task<bool> SendResponseAsync(string responseJson, string clientId = null);
        
        /// <summary>
        /// Event fired when a command is received from a client
        /// </summary>
        event EventHandler<CommandReceivedEventArgs> CommandReceived;
        
        /// <summary>
        /// Event fired when the connection status changes
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
    }
} 