using System;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Settings for establishing connections in different modes
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// The connection mode to use
        /// </summary>
        public ConnectionMode Mode { get; set; }
        
        /// <summary>
        /// For Local mode: TCP port to listen on (default: 1999)
        /// For Remote mode: Not used
        /// </summary>
        public int LocalPort { get; set; } = 1999;
        
        /// <summary>
        /// For Local mode: Host address to bind to (default: 127.0.0.1)
        /// For Remote mode: Not used
        /// </summary>
        public string LocalHost { get; set; } = "127.0.0.1";
        
        /// <summary>
        /// For Remote mode: WebSocket URL of the remote MCP server
        /// For Local mode: Not used
        /// </summary>
        public string RemoteUrl { get; set; }
        
        /// <summary>
        /// For Remote mode: Authentication token for the remote server
        /// For Local mode: Not used
        /// </summary>
        public string AuthToken { get; set; }
        
        /// <summary>
        /// Connection timeout in milliseconds (default: 30 seconds)
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;
        
        /// <summary>
        /// Whether to automatically reconnect on connection loss
        /// </summary>
        public bool AutoReconnect { get; set; } = true;
        
        /// <summary>
        /// Validates the settings for the current mode
        /// </summary>
        /// <returns>True if settings are valid, false otherwise</returns>
        public bool IsValid()
        {
            switch (Mode)
            {
                case ConnectionMode.Local:
                    return LocalPort > 0 && LocalPort <= 65535 && !string.IsNullOrEmpty(LocalHost);
                    
                case ConnectionMode.Remote:
                    // For remote mode, we only need the URL - authentication is handled via license system
                    return !string.IsNullOrEmpty(RemoteUrl);
                    
                default:
                    return false;
            }
        }
    }
} 