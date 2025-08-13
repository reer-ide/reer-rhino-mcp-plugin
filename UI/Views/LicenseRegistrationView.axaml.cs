using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.UI.Views
{
    public partial class LicenseRegistrationView : UserControl
    {
        public event EventHandler<LicenseRegistrationEventArgs> RegistrationCompleted;
        
        private readonly ReerRhinoMCPPlugin _plugin;
        
        public LicenseRegistrationView()
        {
            InitializeComponent();
        }
        
        public LicenseRegistrationView(ReerRhinoMCPPlugin plugin) : this()
        {
            _plugin = plugin;
            SetupEventHandlers();
        }
        
        private void SetupEventHandlers()
        {
            RegisterButton.Click += OnRegisterClick;
            FinishButton.Click += OnFinishClick;
            GetLicenseButton.Click += OnGetLicenseClick;
            
            // Set the server URL based on current settings
            ServerUrlTextBox.Text = ConnectionSettings.GetHttpServerUrl();
            
            // Reset to initial state
            ResetToInitialState();
            
            // Start checking for existing license with 1s delay
            _ = CheckExistingLicenseAsync();
        }
        
        public void ResetToInitialState()
        {
            // Clear form fields
            LicenseKeyTextBox.Text = "";
            UserIdTextBox.Text = "";
            
            // Hide error panel
            ErrorPanel.IsVisible = false;
            
            // Show step 1 (registration form)
            ShowStep(1);
        }
        
        private async Task CheckExistingLicenseAsync()
        {
            // Show checking panel
            ShowCheckingLicense();
            
            // Wait 1 second
            await Task.Delay(1000);
            
            try
            {
                // Check if there's a valid license
                var result = await _plugin.LicenseManager.GetLicenseStatusAsync();
                
                if (result != null && result.IsValid)
                {
                    // License found, notify parent to switch to management view
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RegistrationCompleted?.Invoke(this, new LicenseRegistrationEventArgs 
                        { 
                            LicenseId = result.LicenseId,
                            Success = true 
                        });
                    });
                }
                else
                {
                    // No valid license, show registration form
                    await Dispatcher.UIThread.InvokeAsync(() => ShowStep(1));
                }
            }
            catch
            {
                // Error checking, show registration form
                await Dispatcher.UIThread.InvokeAsync(() => ShowStep(1));
            }
        }
        
        private async void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            var licenseKey = LicenseKeyTextBox.Text?.Trim();
            var userId = UserIdTextBox.Text?.Trim();
            var serverUrl = ConnectionSettings.GetHttpServerUrl();
            
            // Hide error panel when starting new registration
            ErrorPanel.IsVisible = false;
            
            if (string.IsNullOrEmpty(licenseKey) || string.IsNullOrEmpty(userId))
            {
                ShowError("Please enter both License Key and User ID or get one.");
                return;
            }
            
            // Move to Step 2 (Processing)
            ShowStep(2);
            
            try
            {
                RegisterButton.IsEnabled = false;
                
                // Perform registration
                var result = await Task.Run(async () => 
                {
                    return await _plugin.LicenseManager.RegisterLicenseAsync(licenseKey, userId);
                });
                
                if (result.Success)
                {
                    // Update success screen with license ID
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Format the license ID properly or show placeholder
                        if (!string.IsNullOrEmpty(result.LicenseId))
                        {
                            LicenseIdText.Text = FormatLicenseId(result.LicenseId);
                        }
                        else
                        {
                            LicenseIdText.Text = "XXX-XXXXXX-XXXXX";
                        }
                        ShowStep(3);
                    });
                }
                else
                {
                    // Show error and go back to step 1
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        ShowStep(1);
                        ShowError("No valid license found. Please check your Reer integrations or get one.");
                    });
                }
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowStep(1);
                    // Always show the same user-friendly message for license errors
                    ShowError("No valid license found. Please check your Reer integrations or get one.");
                });
            }
            finally
            {
                RegisterButton.IsEnabled = true;
            }
        }
        
        private void OnFinishClick(object sender, RoutedEventArgs e)
        {
            RegistrationCompleted?.Invoke(this, new LicenseRegistrationEventArgs 
            { 
                LicenseId = LicenseIdText.Text,
                Success = true 
            });
        }
        
        private void OnGetLicenseClick(object sender, RoutedEventArgs e)
        {
            // Open the reer.co website in the default browser
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GlobalConstants.REER_WEBSITE_URL,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Fallback for older .NET Framework
                System.Diagnostics.Process.Start(GlobalConstants.REER_WEBSITE_URL);
            }
        }
        
        private void ShowStep(int step)
        {
            // Hide all panels
            CheckingLicensePanel.IsVisible = false;
            Step1Panel.IsVisible = false;
            Step2Panel.IsVisible = false;
            Step3Panel.IsVisible = false;
            
            // Show the requested step
            switch (step)
            {
                case 1:
                    Step1Panel.IsVisible = true;
                    break;
                case 2:
                    Step2Panel.IsVisible = true;
                    break;
                case 3:
                    Step3Panel.IsVisible = true;
                    break;
            }
        }
        
        private void ShowCheckingLicense()
        {
            CheckingLicensePanel.IsVisible = true;
            Step1Panel.IsVisible = false;
            Step2Panel.IsVisible = false;
            Step3Panel.IsVisible = false;
        }
        
        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorPanel.IsVisible = true;
        }
        
        private string FormatLicenseId(string licenseId)
        {
            if (string.IsNullOrEmpty(licenseId))
                return "XXX-XXXXXX-XXXXX";
                
            // Remove any existing formatting
            licenseId = licenseId.Replace("-", "").Replace(" ", "");
            
            // Format as XXX-XXXXXX-XXXXX pattern (14 chars total)
            if (licenseId.Length >= 14)
            {
                return $"{licenseId.Substring(0, 3)}-{licenseId.Substring(3, 6)}-{licenseId.Substring(9, 5)}".ToUpper();
            }
            else if (licenseId.Length > 8)
            {
                // If shorter than 14, show what we have
                return $"{licenseId.Substring(0, 3)}-{licenseId.Substring(3, 6)}-{licenseId.Substring(9)}".ToUpper();
            }
            
            // Return as-is if too short to format
            return licenseId.ToUpper();
        }
    }
    
    public class LicenseRegistrationEventArgs : EventArgs
    {
        public string LicenseId { get; set; }
        public bool Success { get; set; }
    }
}