using System;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Settings for establishing connections in different modes
    /// </summary>
    public class ConnectionSettings
    {
        /// <summary>
        /// Default production server WebSocket URL for remote connections
        /// </summary>
        public const string PRODUCTION_SERVER_URL = "wss://api.reer.ai/mcp";
        
        /// <summary>
        /// Default production server HTTP URL for REST API calls
        /// </summary>
        public const string PRODUCTION_HTTP_URL = "https://api.reer.ai/mcp";
        
        /// <summary>
        /// Default development server WebSocket URL for testing
        /// </summary>
        public const string DEVELOPMENT_SERVER_URL = "ws://127.0.0.1:8080";
        
        /// <summary>
        /// Default development server HTTP URL for REST API calls
        /// </summary>
        public const string DEVELOPMENT_HTTP_URL = "http://127.0.0.1:8080";
        
        /// <summary>
        /// Gets the appropriate WebSocket server URL based on development mode setting
        /// </summary>
        /// <returns>Production or development WebSocket server URL</returns>
        public static string GetServerUrl()
        {
            var settings = ReerRhinoMCPPlugin.Instance?.MCPSettings;
            return settings?.DevelopmentMode == true ? DEVELOPMENT_SERVER_URL : PRODUCTION_SERVER_URL;
        }
        
        /// <summary>
        /// Gets the appropriate HTTP server URL based on development mode setting
        /// </summary>
        /// <returns>Production or development HTTP server URL</returns>
        public static string GetHttpServerUrl()
        {
            var settings = ReerRhinoMCPPlugin.Instance?.MCPSettings;
            return settings?.DevelopmentMode == true ? DEVELOPMENT_HTTP_URL : PRODUCTION_HTTP_URL;
        }
        
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
        /// Gets the HTTP URL for REST API calls (derived from RemoteUrl)
        /// </summary>
        public string RemoteHttpUrl 
        {
            get 
            {
                if (string.IsNullOrEmpty(RemoteUrl))
                    return null;
                    
                return RemoteUrl.Replace("ws://", "http://").Replace("wss://", "https://");
            }
        }
        
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