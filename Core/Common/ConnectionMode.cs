using System;

namespace ReerRhinoMCPPlugin.Core.Common
{
    /// <summary>
    /// Defines the connection modes supported by the plugin
    /// </summary>
    public enum ConnectionMode
    {
        /// <summary>
        /// Local TCP server mode for direct connections (e.g., Claude Desktop)
        /// </summary>
        Local,
        
        /// <summary>
        /// Remote WebSocket client mode for cloud-based connections
        /// </summary>
        Remote
    }
} 