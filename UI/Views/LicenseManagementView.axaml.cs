using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReerRhinoMCPPlugin.Core.Client;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.UI.Views
{
    public partial class LicenseManagementView : UserControl
    {
        public event EventHandler<LicenseActionEventArgs> LicenseActionRequested;
        
        private readonly ReerRhinoMCPPlugin _plugin;
        private LicenseValidationResult _currentLicense;
        
        public LicenseManagementView()
        {
            InitializeComponent();
        }
        
        public LicenseManagementView(ReerRhinoMCPPlugin plugin) : this()
        {
            _plugin = plugin;
            SetupEventHandlers();
            _ = LoadLicenseStatus();
        }
        
        private void SetupEventHandlers()
        {
            UpgradeButton.Click += (s, e) => OpenReerWebsite();
            RenewButton.Click += (s, e) => OpenReerWebsite();
            ContactSupportButton.Click += OnContactSupportClick;
        }
        
        private async Task LoadLicenseStatus()
        {
            try
            {
                var result = await Task.Run(async () => 
                {
                    return await _plugin.LicenseManager.GetLicenseStatusAsync();
                });
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateLicenseDisplay(result);
                });
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Show invalid state if error
                    ValidLicensePanel.IsVisible = false;
                    InvalidLicensePanel.IsVisible = true;
                });
            }
        }
        
        public void UpdateLicenseDisplay(LicenseValidationResult license)
        {
            _currentLicense = license;
            
            if (license == null || string.IsNullOrEmpty(license.LicenseId))
            {
                // No license case is handled by the main window showing NoLicenseView
                return;
            }
            
            if (license.IsValid)
            {
                ShowValidLicense(license);
            }
            else
            {
                ShowInvalidLicense(license);
            }
        }
        
        private void ShowValidLicense(LicenseValidationResult license)
        {
            ValidLicensePanel.IsVisible = true;
            InvalidLicensePanel.IsVisible = false;
            ValidLicenseActions.IsVisible = true;
            InvalidLicenseActions.IsVisible = false;
            
            ValidLicenseIdText.Text = $"License ID: {FormatLicenseId(license.LicenseId)}";
        }
        
        private void ShowInvalidLicense(LicenseValidationResult license)
        {
            ValidLicensePanel.IsVisible = false;
            InvalidLicensePanel.IsVisible = true;
            ValidLicenseActions.IsVisible = false;
            InvalidLicenseActions.IsVisible = true;
            
            InvalidLicenseIdText.Text = $"License ID: {FormatLicenseId(license.LicenseId)}";
        }
        
        private string FormatLicenseId(string licenseId)
        {
            if (string.IsNullOrEmpty(licenseId))
                return "XXX-XXXXXX-XXXXX";
                
            // Format license ID if needed
            if (licenseId.Length > 8)
            {
                return $"{licenseId.Substring(0, 3)}-{licenseId.Substring(3, 6)}-{licenseId.Substring(9)}".ToUpper();
            }
            
            return licenseId.ToUpper();
        }
        
        
        private void OnActionRequested(LicenseAction action)
        {
            LicenseActionRequested?.Invoke(this, new LicenseActionEventArgs { Action = action });
        }
        
        private void OpenReerWebsite()
        {
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
        
        private void OnContactSupportClick(object sender, RoutedEventArgs e)
        {
            // Open email client with support email
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mailto:support@reer.co",
                    UseShellExecute = true
                });
            }
            catch
            {
                System.Diagnostics.Process.Start("mailto:support@reer.co");
            }
        }
    }
    
    public enum LicenseAction
    {
        Register,
        Upgrade,
        Renew
    }
    
    public class LicenseActionEventArgs : EventArgs
    {
        public LicenseAction Action { get; set; }
    }
}