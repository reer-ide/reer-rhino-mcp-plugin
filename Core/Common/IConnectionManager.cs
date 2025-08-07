using System;
using System.Threading.Tasks;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Interface for managing MCP connections and ensuring only one is active at a time
    /// </summary>
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// The currently active connection (null if none)
        /// </summary>
        IRhinoMCPConnection ActiveConnection { get; }
        
        /// <summary>
        /// Indicates whether any connection is currently active
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// The current connection status
        /// </summary>
        ConnectionStatus Status { get; }
        
        /// <summary>
        /// Starts a connection with the specified settings
        /// Will stop any existing connection first
        /// </summary>
        /// <param name="settings">Connection settings to use</param>
        /// <param name="fileValidation">Optional pre-computed file validation result (for remote connections)</param>
        /// <returns>True if connection started successfully, false otherwise</returns>
        Task<bool> StartConnectionAsync(ConnectionSettings settings, FileConnectionValidation fileValidation = null);
        
        /// <summary>
        /// Stops the current connection if one is active
        /// </summary>
        /// <param name="cleanSessionInfo">Whether to clean stored session info (default: true)</param>
        /// <returns>Task representing the async operation</returns>
        Task StopConnectionAsync(bool cleanSessionInfo = true);
        
        /// <summary>
        /// Switches to a different connection mode
        /// Will stop current connection and start new one
        /// </summary>
        /// <param name="settings">New connection settings</param>
        /// <returns>True if switch was successful, false otherwise</returns>
        Task<bool> SwitchConnectionAsync(ConnectionSettings settings);
        
        /// <summary>
        /// Event fired when a command is received from any active connection
        /// </summary>
        event EventHandler<CommandReceivedEventArgs> CommandReceived;
        
        /// <summary>
        /// Event fired when the connection status changes
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> StatusChanged;
        
        /// <summary>
        /// Notifies the server of a file path change (for remote connections)
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="oldPath">The old file path</param>
        /// <param name="newPath">The new file path</param>
        /// <param name="documentGuid">The document GUID</param>
        /// <returns>True if notification was successful, false otherwise</returns>
        Task<bool> NotifyServerOfFilePathChangeAsync(string sessionId, string oldPath, string newPath, string documentGuid);
    }
} 