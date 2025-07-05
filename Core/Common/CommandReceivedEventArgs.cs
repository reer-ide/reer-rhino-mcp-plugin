using System;
using Newtonsoft.Json.Linq;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Event arguments for when a command is received from a client
    /// </summary>
    public class CommandReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// The raw JSON command received from the client
        /// </summary>
        public string RawCommand { get; }
        
        /// <summary>
        /// The parsed command as a JObject
        /// </summary>
        public JObject Command { get; }
        
        /// <summary>
        /// Unique identifier for the client/connection that sent the command
        /// </summary>
        public string ClientId { get; }
        
        /// <summary>
        /// Timestamp when the command was received
        /// </summary>
        public DateTime ReceivedAt { get; }
        
        public CommandReceivedEventArgs(string rawCommand, JObject command, string clientId)
        {
            RawCommand = rawCommand ?? throw new ArgumentNullException(nameof(rawCommand));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            ReceivedAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Event arguments for connection status changes
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The current connection status
        /// </summary>
        public ConnectionStatus Status { get; }
        
        /// <summary>
        /// Optional message describing the status change
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Exception that caused the status change (if applicable)
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// Timestamp when the status changed
        /// </summary>
        public DateTime ChangedAt { get; }
        
        public ConnectionStatusChangedEventArgs(ConnectionStatus status, string message = null, Exception exception = null)
        {
            Status = status;
            Message = message;
            Exception = exception;
            ChangedAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Represents the status of a connection
    /// </summary>
    public enum ConnectionStatus
    {
        /// <summary>
        /// Not connected
        /// </summary>
        Disconnected,
        
        /// <summary>
        /// Attempting to connect
        /// </summary>
        Connecting,
        
        /// <summary>
        /// Successfully connected
        /// </summary>
        Connected,
        
        /// <summary>
        /// Connection lost, attempting to reconnect
        /// </summary>
        Reconnecting,
        
        /// <summary>
        /// Connection failed
        /// </summary>
        Failed,
        
        /// <summary>
        /// Connection error
        /// </summary>
        Error
    }
} 