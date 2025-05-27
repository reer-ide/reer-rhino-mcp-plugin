using System;
using System.IO;
using Newtonsoft.Json;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Config
{
    /// <summary>
    /// Manages persistent settings for the Rhino MCP Plugin
    /// </summary>
    public class RhinoMCPSettings
    {
        private const string SETTINGS_KEY = "ReerRhinoMCPPlugin_Settings";
        private static readonly object lockObject = new object();
        
        /// <summary>
        /// Default connection settings
        /// </summary>
        public ConnectionSettings DefaultConnection { get; set; } = new ConnectionSettings
        {
            Mode = ConnectionMode.Local
        };
        
        /// <summary>
        /// Whether to auto-start connection when plugin loads
        /// </summary>
        public bool AutoStart { get; set; } = false;
        
        /// <summary>
        /// Whether to show connection status in Rhino's status bar
        /// </summary>
        public bool ShowStatusBar { get; set; } = true;
        
        /// <summary>
        /// Whether to log debug information
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;
        
        /// <summary>
        /// Last used connection mode for convenience
        /// </summary>
        public ConnectionMode LastUsedMode { get; set; } = ConnectionMode.Local;
        
        /// <summary>
        /// Saves the current settings to Rhino's persistent storage
        /// </summary>
        public void Save()
        {
            lock (lockObject)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                    
                    // Use Rhino's plugin settings system
                    var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
                    if (plugin != null)
                    {
                        plugin.Settings.SetString(SETTINGS_KEY, json);
                        RhinoApp.WriteLine("RhinoMCP settings saved successfully.");
                    }
                    else
                    {
                        RhinoApp.WriteLine("Plugin instance not available for saving settings.");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Failed to save RhinoMCP settings: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Loads settings from Rhino's persistent storage
        /// </summary>
        /// <returns>Loaded settings or default settings if none exist</returns>
        public static RhinoMCPSettings Load()
        {
            lock (lockObject)
            {
                try
                {
                    var plugin = rhino_mcp_plugin.ReerRhinoMCPPlugin.Instance;
                    if (plugin == null)
                    {
                        RhinoApp.WriteLine("Plugin instance not available for loading settings, using defaults.");
                        return new RhinoMCPSettings();
                    }
                    
                    string json = plugin.Settings.GetString(SETTINGS_KEY, null);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        // Return default settings if none saved
                        var defaultSettings = new RhinoMCPSettings();
                        RhinoApp.WriteLine("No saved RhinoMCP settings found, using defaults.");
                        return defaultSettings;
                    }
                    
                    var settings = JsonConvert.DeserializeObject<RhinoMCPSettings>(json);
                    RhinoApp.WriteLine("RhinoMCP settings loaded successfully.");
                    return settings;
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Failed to load RhinoMCP settings: {ex.Message}");
                    return new RhinoMCPSettings(); // Return defaults on error
                }
            }
        }
        
        /// <summary>
        /// Resets settings to defaults
        /// </summary>
        public void Reset()
        {
            DefaultConnection = new ConnectionSettings { Mode = ConnectionMode.Local };
            AutoStart = false;
            ShowStatusBar = true;
            EnableDebugLogging = false;
            LastUsedMode = ConnectionMode.Local;
        }
        
        /// <summary>
        /// Validates the current settings
        /// </summary>
        /// <returns>True if settings are valid, false otherwise</returns>
        public bool IsValid()
        {
            return DefaultConnection != null && DefaultConnection.IsValid();
        }
        
        /// <summary>
        /// Creates a copy of the default connection settings
        /// </summary>
        /// <returns>Copy of the default connection settings</returns>
        public ConnectionSettings GetDefaultConnectionSettings()
        {
            return new ConnectionSettings
            {
                Mode = DefaultConnection.Mode,
                LocalHost = DefaultConnection.LocalHost,
                LocalPort = DefaultConnection.LocalPort,
                RemoteUrl = DefaultConnection.RemoteUrl,
                AuthToken = DefaultConnection.AuthToken,
                TimeoutMs = DefaultConnection.TimeoutMs,
                AutoReconnect = DefaultConnection.AutoReconnect
            };
        }
    }
} 