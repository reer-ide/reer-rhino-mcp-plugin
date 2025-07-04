using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;

namespace ReerRhinoMCPPlugin.UI.ViewModels
{
    public class MCPControlPanelViewModel : INotifyPropertyChanged
    {
        private readonly rhino_mcp_plugin.ReerRhinoMCPPlugin _plugin;
        private readonly RhinoMCPSettings _settings;
        private bool _isConnected;
        private bool _isLoading;
        private string _connectionStatusText = "Disconnected";
        private IBrush _statusColor = Brushes.Red;
        private int _clientCount;
        private string _uptime = "00:00:00";
        private DateTime _startTime;
        private string _serverPort = "1999";
        private bool _autoStart;
        private bool _enableDebugLogging;
        private bool _showStatusBar;

        public MCPControlPanelViewModel(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _settings = plugin.MCPSettings;
            
            // Initialize commands
            StartServerCommand = new AsyncRelayCommand(StartServerAsync, () => !IsConnected && !IsLoading);
            StopServerCommand = new AsyncRelayCommand(StopServerAsync, () => IsConnected && !IsLoading);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ClearLogCommand = new RelayCommand(ClearLog);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenDocumentationCommand = new RelayCommand(OpenDocumentation);
            OpenGitHubCommand = new RelayCommand(OpenGitHub);
            
            // Initialize log entries
            LogEntries = new ObservableCollection<LogEntry>();
            
            // Load current settings
            LoadSettings();
            
            // Subscribe to connection events
            if (_plugin.ConnectionManager != null)
            {
                _plugin.ConnectionManager.StatusChanged += OnConnectionStatusChanged;
                _plugin.ConnectionManager.CommandReceived += OnCommandReceived;
                
                // Update initial status
                UpdateConnectionStatus();
            }
            
            // Add welcome message
            AddLogEntry("INFO", "MCP Control Panel initialized");
        }

        #region Properties

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(IsDisconnected));
                    ((AsyncRelayCommand)StartServerCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)StopServerCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsDisconnected => !IsConnected;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    ((AsyncRelayCommand)StartServerCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)StopServerCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
        }

        public IBrush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public int ClientCount
        {
            get => _clientCount;
            set => SetProperty(ref _clientCount, value);
        }

        public string CurrentPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }

        public string ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        public bool AutoStart
        {
            get => _autoStart;
            set => SetProperty(ref _autoStart, value);
        }

        public bool EnableDebugLogging
        {
            get => _enableDebugLogging;
            set => SetProperty(ref _enableDebugLogging, value);
        }

        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set => SetProperty(ref _showStatusBar, value);
        }

        public ObservableCollection<LogEntry> LogEntries { get; }

        #endregion

        #region Commands

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenDocumentationCommand { get; }
        public ICommand OpenGitHubCommand { get; }

        #endregion

        #region Command Implementations

        private async Task StartServerAsync()
        {
            try
            {
                IsLoading = true;
                AddLogEntry("INFO", "Starting MCP server...");

                var connectionSettings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalPort = int.TryParse(ServerPort, out int port) ? port : 1999
                };

                bool success = await _plugin.ConnectionManager.StartConnectionAsync(connectionSettings);
                
                if (success)
                {
                    _startTime = DateTime.Now;
                    AddLogEntry("SUCCESS", $"MCP server started on port {connectionSettings.LocalPort}");
                    StartUptimeTimer();
                }
                else
                {
                    AddLogEntry("ERROR", "Failed to start MCP server");
                }
            }
            catch (Exception ex)
            {
                AddLogEntry("ERROR", $"Error starting server: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task StopServerAsync()
        {
            try
            {
                IsLoading = true;
                AddLogEntry("INFO", "Stopping MCP server...");

                await _plugin.ConnectionManager.StopConnectionAsync();
                AddLogEntry("SUCCESS", "MCP server stopped");
            }
            catch (Exception ex)
            {
                AddLogEntry("ERROR", $"Error stopping server: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.DefaultConnection.LocalPort = int.TryParse(ServerPort, out int port) ? port : 1999;
                _settings.AutoStart = AutoStart;
                _settings.EnableDebugLogging = EnableDebugLogging;
                _settings.ShowStatusBar = ShowStatusBar;
                
                _settings.Save();
                AddLogEntry("SUCCESS", "Settings saved successfully");
            }
            catch (Exception ex)
            {
                AddLogEntry("ERROR", $"Error saving settings: {ex.Message}");
            }
        }

        private void ClearLog()
        {
            LogEntries.Clear();
            AddLogEntry("INFO", "Log cleared");
        }

        private void OpenSettings()
        {
            // TODO: Open advanced settings dialog
            AddLogEntry("INFO", "Advanced settings dialog - Coming soon!");
        }

        private void OpenDocumentation()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/reer/rhino-mcp-plugin/docs",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AddLogEntry("ERROR", $"Error opening documentation: {ex.Message}");
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
                AddLogEntry("ERROR", $"Error opening GitHub: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            // Update UI on main thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateConnectionStatus();
                AddLogEntry("INFO", $"Connection status: {e.Status} - {e.Message}");
            });
        }

        private void OnCommandReceived(object sender, CommandReceivedEventArgs e)
        {
            // Update UI on main thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                string commandType = e.Command["type"]?.ToString() ?? "unknown";
                AddLogEntry("COMMAND", $"Received command: {commandType} from {e.ClientId}");
            });
        }

        #endregion

        #region Helper Methods

        private void LoadSettings()
        {
            ServerPort = _settings.DefaultConnection.LocalPort.ToString();
            AutoStart = _settings.AutoStart;
            EnableDebugLogging = _settings.EnableDebugLogging;
            ShowStatusBar = _settings.ShowStatusBar;
        }

        private void UpdateConnectionStatus()
        {
            if (_plugin.ConnectionManager == null)
            {
                IsConnected = false;
                ConnectionStatusText = "Not Available";
                StatusColor = Brushes.Gray;
                return;
            }

            IsConnected = _plugin.ConnectionManager.IsConnected;
            
            switch (_plugin.ConnectionManager.Status)
            {
                case ConnectionStatus.Connected:
                    ConnectionStatusText = "Connected";
                    StatusColor = Brushes.Green;
                    break;
                case ConnectionStatus.Connecting:
                    ConnectionStatusText = "Connecting...";
                    StatusColor = Brushes.Orange;
                    break;
                case ConnectionStatus.Disconnected:
                    ConnectionStatusText = "Disconnected";
                    StatusColor = Brushes.Red;
                    break;
                case ConnectionStatus.Failed:
                    ConnectionStatusText = "Failed";
                    StatusColor = Brushes.Red;
                    break;
                case ConnectionStatus.Error:
                    ConnectionStatusText = "Error";
                    StatusColor = Brushes.Red;
                    break;
                case ConnectionStatus.Reconnecting:
                    ConnectionStatusText = "Reconnecting...";
                    StatusColor = Brushes.Orange;
                    break;
                default:
                    ConnectionStatusText = "Unknown";
                    StatusColor = Brushes.Gray;
                    break;
            }
        }

        private void StartUptimeTimer()
        {
            // Simple uptime calculation - in a real app, you'd use a proper timer
            Task.Run(async () =>
            {
                while (IsConnected)
                {
                    await Task.Delay(1000);
                    var elapsed = DateTime.Now - _startTime;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Uptime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                    });
                }
            });
        }

        public void AddLogEntry(string level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Level = level,
                Message = message,
                LevelColor = GetLevelColor(level)
            };

            LogEntries.Insert(0, entry);
            
            // Keep only last 100 entries
            while (LogEntries.Count > 100)
            {
                LogEntries.RemoveAt(LogEntries.Count - 1);
            }
        }

        private IBrush GetLevelColor(string level)
        {
            return level switch
            {
                "ERROR" => Brushes.Red,
                "SUCCESS" => Brushes.Green,
                "COMMAND" => Brushes.Blue,
                "INFO" => Brushes.Gray,
                _ => Brushes.Black
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    #region Helper Classes

    public class LogEntry
    {
        public string Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public IBrush LevelColor { get; set; }
        public IBrush Background { get; set; } = Brushes.Transparent;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
} 