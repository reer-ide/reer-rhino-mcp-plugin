using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ReerRhinoMCPPlugin.UI.ViewModels.Base;
using ReerRhinoMCPPlugin.UI.ViewModels.Commands;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Config;

namespace ReerRhinoMCPPlugin.UI.ViewModels
{
    /// <summary>
    /// ViewModel for server control operations
    /// </summary>
    public class ServerControlViewModel : ViewModelBase
    {
        private readonly IConnectionManager _connectionManager;
        private bool _isLoading;
        private bool _isConnected;
        private string _serverPort = "1999";

        public ServerControlViewModel(IConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            
            StartServerCommand = new AsyncRelayCommand(StartServerAsync, () => !IsConnected && !IsLoading);
            StopServerCommand = new AsyncRelayCommand(StopServerAsync, () => IsConnected && !IsLoading);
        }

        #region Properties

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

        public string ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        #endregion

        #region Commands

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }

        #endregion

        #region Command Implementations

        private async Task StartServerAsync()
        {
            try
            {
                IsLoading = true;

                var connectionSettings = new ConnectionSettings
                {
                    Mode = ConnectionMode.Local,
                    LocalPort = int.TryParse(ServerPort, out int port) ? port : 1999
                };

                bool success = await _connectionManager.StartConnectionAsync(connectionSettings);
                
                if (success)
                {
                    IsConnected = true;
                    OnServerStarted?.Invoke(connectionSettings.LocalPort);
                }
                else
                {
                    OnServerStartFailed?.Invoke("Failed to start server");
                }
            }
            catch (Exception ex)
            {
                OnServerStartFailed?.Invoke(ex.Message);
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
                await _connectionManager.StopConnectionAsync();
                IsConnected = false;
                OnServerStopped?.Invoke();
            }
            catch (Exception ex)
            {
                OnServerStopFailed?.Invoke(ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Events

        public event Action<int> OnServerStarted;
        public event Action<string> OnServerStartFailed;
        public event Action OnServerStopped;
        public event Action<string> OnServerStopFailed;

        #endregion
    }
}
