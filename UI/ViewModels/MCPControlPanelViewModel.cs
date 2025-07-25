using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ReerRhinoMCPPlugin.UI.ViewModels.Base;
using ReerRhinoMCPPlugin.UI.ViewModels.Commands;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Config;
using Rhino;

namespace ReerRhinoMCPPlugin.UI.ViewModels
{
    /// <summary>
    /// Simplified main ViewModel that coordinates child ViewModels
    /// </summary>
    public class MCPControlPanelViewModel : ViewModelBase, IDisposable
    {
        private readonly ReerRhinoMCPPlugin _plugin;
        private readonly RhinoMCPSettings _settings;
        private bool _keepOnTop = true;
        private DateTime _startTime;
        private CancellationTokenSource _uptimeCancellationTokenSource;

        public MCPControlPanelViewModel(ReerRhinoMCPPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _settings = plugin.MCPSettings;

            // Initialize child ViewModels
            ConnectionStatus = new ConnectionStatusViewModel();
            ServerControl = new ServerControlViewModel(plugin.ConnectionManager);
            LogViewer = new LogViewModel();

            // Initialize commands
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenDocumentationCommand = new RelayCommand(OpenDocumentation);
            OpenGitHubCommand = new RelayCommand(OpenGitHub);

            // Subscribe to events
            SubscribeToEvents();

            // Load settings
            LoadSettings();
        }

        #region Properties

        public ConnectionStatusViewModel ConnectionStatus { get; }
        public ServerControlViewModel ServerControl { get; }
        public LogViewModel LogViewer { get; }

        public bool KeepOnTop
        {
            get => _keepOnTop;
            set
            {
                if (SetProperty(ref _keepOnTop, value))
                {
                    KeepOnTopChanged?.Invoke(this, value);
                }
            }
        }

        #endregion

        #region Commands

        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenDocumentationCommand { get; }
        public ICommand OpenGitHubCommand { get; }

        #endregion

        #region Events

        public event EventHandler<bool> KeepOnTopChanged;

        #endregion

        #region Private Methods

        private void SubscribeToEvents()
        {
            // Subscribe to connection manager events
            if (_plugin.ConnectionManager != null)
            {
                _plugin.ConnectionManager.StatusChanged += OnConnectionStatusChanged;
                _plugin.ConnectionManager.CommandReceived += OnCommandReceived;
            }

            // Subscribe to server control events
            ServerControl.OnServerStarted += OnServerStarted;
            ServerControl.OnServerStartFailed += OnServerStartFailed;
            ServerControl.OnServerStopped += OnServerStopped;
            ServerControl.OnServerStopFailed += OnServerStopFailed;
        }

        private void LoadSettings()
        {
            ServerControl.ServerPort = _settings.DefaultConnection.LocalPort.ToString();
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            // Update connection status
            ConnectionStatus.UpdateStatus(e.Status, e.Message);
            ServerControl.IsConnected = e.Status == Core.Common.ConnectionStatus.Connected;

            // Add log entry
            string logLevel = e.Status == Core.Common.ConnectionStatus.Connected ? "SUCCESS" : "INFO";
            LogViewer.AddLogEntry(logLevel, $"Connection status: {e.Status} - {e.Message}");

            // Update client count if available
            if (e.Status == Core.Common.ConnectionStatus.Connected && _plugin.ConnectionManager is RhinoMCPConnectionManager manager)
            {
                // Get client count from server if it's a local connection
                // This would need to be implemented in the connection manager
            }
        }

        private void OnCommandReceived(object sender, CommandReceivedEventArgs e)
        {
            // Handle command received events if needed
            string commandType = e.Command["type"]?.ToString() ?? "unknown";
            LogViewer.AddLogEntry("COMMAND", $"Received command: {commandType} from {e.ClientId}");
            Logger.Info($"[UI] Command received: {commandType} from {e.ClientId}");
        }

        private void OnServerStarted(int port)
        {
            _startTime = DateTime.Now;
            ConnectionStatus.CurrentPort = port.ToString();
            StartUptimeTimer();
            LogViewer.AddLogEntry("SUCCESS", $"MCP server started on port {port}");
            Logger.Info($"[UI] Server started on port {port}");
        }

        private void OnServerStartFailed(string error)
        {
            LogViewer.AddLogEntry("ERROR", $"Failed to start MCP server: {error}");
            Logger.Error($"[UI] Server start failed: {error}");
        }

        private void OnServerStopped()
        {
            StopUptimeTimer();
            // Reset connection status when server stops
            ConnectionStatus.UpdateStatus(Core.Common.ConnectionStatus.Disconnected, "Server stopped");
            ConnectionStatus.CurrentPort = "";
            ConnectionStatus.ClientCount = 0;
            LogViewer.AddLogEntry("SUCCESS", "MCP server stopped successfully");
            Logger.Info("[UI] Server stopped");
        }

        private void OnServerStopFailed(string error)
        {
            LogViewer.AddLogEntry("ERROR", $"Error stopping server: {error}");
            Logger.Error($"[UI] Server stop failed: {error}");
        }

        private void StartUptimeTimer()
        {
            StopUptimeTimer();
            _uptimeCancellationTokenSource = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                while (!_uptimeCancellationTokenSource.Token.IsCancellationRequested)
                {
                    var uptime = DateTime.Now - _startTime;
                    ConnectionStatus.Uptime = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
                    
                    try
                    {
                        await Task.Delay(1000, _uptimeCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _uptimeCancellationTokenSource.Token);
        }

        private void StopUptimeTimer()
        {
            _uptimeCancellationTokenSource?.Cancel();
            _uptimeCancellationTokenSource?.Dispose();
            _uptimeCancellationTokenSource = null;
        }

        #endregion

        #region Command Implementations

        private void OpenSettings()
        {
            // TODO: Implement settings dialog
            Logger.Info("[UI] Settings dialog not implemented yet");
        }

        private void OpenDocumentation()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/reer/rhino-mcp-plugin/wiki",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[UI] Failed to open documentation: {ex.Message}");
            }
        }

        private void OpenGitHub()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/reer/rhino-mcp-plugin",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[UI] Failed to open GitHub: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopUptimeTimer();

            // Unsubscribe from events
            if (_plugin.ConnectionManager != null)
            {
                _plugin.ConnectionManager.StatusChanged -= OnConnectionStatusChanged;
                _plugin.ConnectionManager.CommandReceived -= OnCommandReceived;
            }
        }

        #endregion
    }
}
