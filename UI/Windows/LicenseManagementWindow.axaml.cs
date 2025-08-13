using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReerRhinoMCPPlugin.Core.Client;
using ReerRhinoMCPPlugin.UI.Views;
using Rhino;

namespace ReerRhinoMCPPlugin.UI.Windows
{
    public partial class LicenseManagementWindow : Window
    {
        private readonly ReerRhinoMCPPlugin _plugin;
        private LicenseRegistrationView _registrationView;
        private LicenseManagementView _managementView;
        private RemoveLicenseConfirmationView _removeConfirmationView;
        private UserControl _currentView;
        private bool _logPanelVisible = false;

        public LicenseManagementWindow(ReerRhinoMCPPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponent();
            SetupWindowIcon();
            SetupViews();
            SetupEventHandlers();
            
            // Automatically check license status on startup
            _ = Task.Run(CheckLicenseStatusOnStartup);
        }
        
        private void SetupWindowIcon()
        {
            try
            {
                // Load icon from Avalonia resources - using PNG for window icon as SVG isn't supported for window icons
                var iconStream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://ReerConnector/EmbeddedResources/ReerIcon.png"));
                this.Icon = new WindowIcon(iconStream);
            }
            catch { }
        }

        private void SetupViews()
        {
            // Initialize views
            _registrationView = new LicenseRegistrationView(_plugin);
            _managementView = new LicenseManagementView(_plugin);
            _removeConfirmationView = new RemoveLicenseConfirmationView(_plugin);
            
            // Subscribe to view events
            _registrationView.RegistrationCompleted += OnRegistrationCompleted;
            _managementView.LicenseActionRequested += OnLicenseActionRequested;
            _removeConfirmationView.RemoveLicenseRequested += OnRemoveLicenseRequested;
        }

        private void SetupEventHandlers()
        {
            ToggleLogButton.Click += OnToggleLogClick;
            CloseLogButton.Click += OnCloseLogClick;
            ClearLogButton.Click += OnClearLogClick;
        }

        private async Task CheckLicenseStatusOnStartup()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogMessage("🔄 Checking existing license...");
            });

            await RefreshLicenseStatus();
        }
        
        private async Task RefreshLicenseStatus()
        {
            try
            {
                var licenseResult = await _plugin.LicenseManager.GetLicenseStatusAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (licenseResult != null && !string.IsNullOrEmpty(licenseResult.LicenseId))
                    {
                        // Has license (valid or invalid)
                        // Recreate management view to ensure fresh state
                        _managementView = new LicenseManagementView(_plugin);
                        _managementView.LicenseActionRequested += OnLicenseActionRequested;
                        
                        ShowManagementView();
                        _managementView.UpdateLicenseDisplay(licenseResult);
                        
                        if (licenseResult.IsValid)
                        {
                            LogMessage("✅ Valid license found");
                        }
                        else
                        {
                            LogMessage("⚠️ License is invalid or expired");
                        }
                    }
                    else
                    {
                        // No license at all - show registration
                        // Recreate registration view to ensure fresh state
                        _registrationView = new LicenseRegistrationView(_plugin);
                        _registrationView.RegistrationCompleted += OnRegistrationCompleted;
                        
                        ShowRegistrationView();
                        LogMessage("📦 No license found - showing registration");
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogMessage($"⚠️ Could not check license status: {ex.Message}");
                    // If error checking, assume no license and show registration
                    // Recreate registration view to ensure fresh state
                    _registrationView = new LicenseRegistrationView(_plugin);
                    _registrationView.RegistrationCompleted += OnRegistrationCompleted;
                    
                    ShowRegistrationView();
                });
            }
        }

        private void ShowRegistrationView()
        {
            if (_currentView != _registrationView)
            {
                _currentView = _registrationView;
                ViewContainer.Content = _registrationView;
                LogMessage("📦 Showing registration view");
            }
        }

        private void ShowManagementView()
        {
            if (_currentView != _managementView)
            {
                _currentView = _managementView;
                ViewContainer.Content = _managementView;
                LogMessage("🔑 Showing license management view");
            }
        }
        
        // NoLicenseView is no longer used - we show registration instead

        private async void OnRegistrationCompleted(object sender, LicenseRegistrationEventArgs e)
        {
            if (e.Success)
            {
                LogMessage($"✅ Registration completed successfully! License ID: {e.LicenseId}");
                
                // Refresh the UI based on actual license status
                await RefreshLicenseStatus();
            }
            else
            {
                LogMessage("❌ Registration failed");
            }
        }

        private void OnLicenseActionRequested(object sender, LicenseActionEventArgs e)
        {
            switch (e.Action)
            {
                case LicenseAction.Register:
                    ShowRegistrationView();
                    break;
                case LicenseAction.Upgrade:
                    LogMessage("🚀 Opening upgrade page...");
                    // TODO: Open upgrade URL in browser
                    break;
                case LicenseAction.Renew:
                    LogMessage("🔄 Opening renewal page...");
                    // TODO: Open renewal URL in browser
                    break;
                case LicenseAction.RemoveConfirmation:
                    ShowRemoveConfirmationView();
                    break;
            }
        }
        
        private async void OnRemoveLicenseRequested(object sender, RemoveLicenseEventArgs e)
        {
            if (e.Action == RemoveLicenseAction.Removed)
            {
                LogMessage("🗑️ License removed successfully");
                // After removing, check the actual license status and update UI accordingly
                await RefreshLicenseStatus();
            }
            else if (e.Action == RemoveLicenseAction.Cancelled)
            {
                LogMessage("❌ License removal cancelled");
                // Go back and refresh the license status
                await RefreshLicenseStatus();
            }
        }
        
        private void ShowRemoveConfirmationView()
        {
            if (_currentView != _removeConfirmationView)
            {
                _currentView = _removeConfirmationView;
                ViewContainer.Content = _removeConfirmationView;
                LogMessage("⚠️ Showing end user license agreement confirmation");
            }
        }

        private void OnToggleLogClick(object sender, RoutedEventArgs e)
        {
            _logPanelVisible = !_logPanelVisible;
            ActivityLogPanel.IsVisible = _logPanelVisible;
            
            if (_logPanelVisible)
            {
                if (ToggleLogButton.Content is TextBlock tb)
                {
                    tb.Text = "✕";
                }
                LogMessage("📋 Activity log opened");
            }
            else
            {
                if (ToggleLogButton.Content is TextBlock tb)
                {
                    tb.Text = "📋";
                }
            }
        }

        private void OnCloseLogClick(object sender, RoutedEventArgs e)
        {
            _logPanelVisible = false;
            ActivityLogPanel.IsVisible = false;
            if (ToggleLogButton.Content is TextBlock tb)
            {
                tb.Text = "📋";
            }
        }

        private void OnClearLogClick(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = "Ready...";
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}";
            
            if (LogTextBlock != null)
            {
                if (LogTextBlock.Text == "Ready...")
                {
                    LogTextBlock.Text = logEntry;
                }
                else
                {
                    LogTextBlock.Text += $"\n{logEntry}";
                }
                
                // Auto-scroll to bottom if log panel is visible
                if (_logPanelVisible && LogTextBlock.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToEnd();
                }
            }
        }

        // Placeholder for any additional helper methods
        // The confirmation dialog functionality is now handled within the views
    }
}


