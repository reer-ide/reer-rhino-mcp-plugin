using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReerRhinoMCPPlugin.Core.Client;
using Rhino;

namespace ReerRhinoMCPPlugin.UI.Windows
{
    public partial class LicenseManagementWindow : Window
    {
        private readonly ReerRhinoMCPPlugin _plugin;
        private bool _hasValidLicense = false;

        public LicenseManagementWindow(ReerRhinoMCPPlugin plugin)
        {
            _plugin = plugin;
            InitializeComponent();
            SetupEventHandlers();
            
            // Automatically check license status on startup
            _ = Task.Run(CheckLicenseStatusOnStartup);
        }

        private void SetupEventHandlers()
        {
            RegisterButton.Click += OnRegisterClick;
            CheckStatusButton.Click += OnCheckStatusClick;
            ClearLicenseButton.Click += OnClearLicenseClick;
            ClearLogButton.Click += OnClearLogClick;
        }

        private async Task CheckLicenseStatusOnStartup()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogMessage("🔄 Checking existing license...");
            });

            try
            {
                var licenseResult = await _plugin.LicenseManager.GetLicenseStatusAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateUIBasedOnLicenseStatus(licenseResult);
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogMessage($"⚠️ Could not check license status: {ex.Message}");
                    ShowRegistrationForm();
                });
            }
        }

        private void UpdateUIBasedOnLicenseStatus(LicenseValidationResult licenseResult)
        {
            if (licenseResult.IsValid)
            {
                _hasValidLicense = true;
                ShowLicenseInfo(licenseResult);
                LogMessage("✅ Valid license found");
            }
            else
            {
                _hasValidLicense = false;
                ShowRegistrationForm();
                LogMessage("❌ No valid license found - please register");
            }
        }

        private void ShowLicenseInfo(LicenseValidationResult licenseResult)
        {
            // Hide registration form
            RegistrationPanel.IsVisible = false;
            
            // Show license information
            LicenseInfoPanel.IsVisible = true;
            
            var statusText = $"✅ License Status: ACTIVE\n" +
                           $"License ID: {licenseResult.LicenseId}\n" +
                           $"User ID: {licenseResult.UserId}\n" +
                           $"Tier: {licenseResult.Tier}\n" +
                           $"Max Files: {licenseResult.MaxConcurrentFiles}";

            StatusTextBlock.Text = statusText;
            
            // Update button states
            CheckStatusButton.Content = "Refresh Status";
            ClearLicenseButton.IsVisible = true;
        }

        private void ShowRegistrationForm()
        {
            // Show registration form
            RegistrationPanel.IsVisible = true;
            
            // Hide license information
            LicenseInfoPanel.IsVisible = false;
            
            StatusTextBlock.Text = "No license registered. Please register below.";
            
            // Update button states
            CheckStatusButton.Content = "Check Status";
            ClearLicenseButton.IsVisible = false;
        }

        private async void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var licenseKey = LicenseKeyTextBox.Text?.Trim();
                var userId = UserIdTextBox.Text?.Trim();
                var serverUrl = ServerUrlTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(licenseKey) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(serverUrl))
                {
                    LogMessage("❌ Please fill in all fields");
                    return;
                }

                LogMessage("🔄 Registering license...");
                RegisterButton.IsEnabled = false;
                RegisterButton.Content = "Registering...";

                await Task.Run(async () =>
                {
                    try
                    {
                        var result = await _plugin.LicenseManager.RegisterLicenseAsync(licenseKey, userId);

                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            if (result.Success)
                            {
                                LogMessage($"✅ License registration completed successfully! License ID: {result.LicenseId}");
                                LicenseKeyTextBox.Text = "";
                                
                                // Re-check status and update UI after successful registration
                                LogMessage("🔄 Refreshing license status...");
                                await CheckLicenseStatusOnStartup();
                            }
                            else
                            {
                                LogMessage("❌ License registration failed");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LogMessage($"❌ Registration error: {ex.Message}");
                        });
                    }
                    finally
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            RegisterButton.IsEnabled = true;
                            RegisterButton.Content = "Register License";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error: {ex.Message}");
                RegisterButton.IsEnabled = true;
                RegisterButton.Content = "Register License";
            }
        }

        private async void OnCheckStatusClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("🔄 Checking license status...");
                CheckStatusButton.IsEnabled = false;
                var originalContent = CheckStatusButton.Content;
                CheckStatusButton.Content = "Checking...";

                await Task.Run(async () =>
                {
                    try
                    {
                        var licenseResult = await _plugin.LicenseManager.GetLicenseStatusAsync();

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            UpdateUIBasedOnLicenseStatus(licenseResult);
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LogMessage($"❌ Error checking license: {ex.Message}");
                            StatusTextBlock.Text = $"❌ Error: {ex.Message}";
                        });
                    }
                    finally
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            CheckStatusButton.IsEnabled = true;
                            CheckStatusButton.Content = originalContent;
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error: {ex.Message}");
                CheckStatusButton.IsEnabled = true;
            }
        }

        private async void OnClearLicenseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await ShowConfirmationDialog("Clear License", 
                    "Are you sure you want to clear the stored license?\nYou will need to register again to use remote connections.");

                if (!result)
                {
                    LogMessage("License clear cancelled");
                    return;
                }

                LogMessage("🔄 Clearing stored license...");
                
                // Clear stored license using LicenseManager
                _plugin.LicenseManager.ClearStoredLicense();
                
                LogMessage("✅ License cleared successfully");
                
                // Show registration form after clearing
                ShowRegistrationForm();
                
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error clearing license: {ex.Message}");
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
            
            if (LogTextBlock.Text == "Ready...")
            {
                LogTextBlock.Text = logEntry;
            }
            else
            {
                LogTextBlock.Text += $"\n{logEntry}";
            }
        }

        private async Task<bool> ShowConfirmationDialog(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var result = false;
            var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
            
            panel.Children.Add(new TextBlock 
            { 
                Text = message, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel 
            { 
                Orientation = Avalonia.Layout.Orientation.Horizontal, 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10
            };

            var yesButton = new Button { Content = "Yes", Padding = new Avalonia.Thickness(20, 8) };
            var noButton = new Button { Content = "No", Padding = new Avalonia.Thickness(20, 8) };

            yesButton.Click += (s, e) => { result = true; dialog.Close(); };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(noButton);
            buttonPanel.Children.Add(yesButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);
            
            return result;
        }
    }
}


