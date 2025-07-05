using System;
using Avalonia.Media;
using ReerRhinoMCPPlugin.UI.ViewModels.Base;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.UI.ViewModels
{
    /// <summary>
    /// ViewModel for connection status display
    /// </summary>
    public class ConnectionStatusViewModel : ViewModelBase
    {
        private bool _isConnected;
        private string _statusText = "Disconnected";
        private IBrush _statusColor = Brushes.Red;
        private int _clientCount;
        private string _uptime = "00:00:00";
        private string _currentPort = "N/A";

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
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

        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }

        public string CurrentPort
        {
            get => _currentPort;
            set => SetProperty(ref _currentPort, value);
        }

        /// <summary>
        /// Updates the connection status based on the provided status
        /// </summary>
        public void UpdateStatus(ConnectionStatus status, string message = null)
        {
            switch (status)
            {
                case ConnectionStatus.Connected:
                    IsConnected = true;
                    StatusText = "Connected";
                    StatusColor = Brushes.Green;
                    break;
                case ConnectionStatus.Connecting:
                    IsConnected = false;
                    StatusText = "Connecting...";
                    StatusColor = Brushes.Orange;
                    break;
                case ConnectionStatus.Disconnected:
                    IsConnected = false;
                    StatusText = "Disconnected";
                    StatusColor = Brushes.Red;
                    ClientCount = 0;
                    Uptime = "00:00:00";
                    CurrentPort = "N/A";
                    break;
                case ConnectionStatus.Error:
                    IsConnected = false;
                    StatusText = $"Error: {message ?? "Unknown error"}";
                    StatusColor = Brushes.Red;
                    break;
            }
        }
    }
}
