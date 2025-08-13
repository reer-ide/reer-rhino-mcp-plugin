using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.UI.Views
{
    public partial class NoLicenseView : UserControl
    {
        public event EventHandler GetLicenseRequested;
        
        public NoLicenseView()
        {
            InitializeComponent();
            SetupEventHandlers();
        }
        
        private void SetupEventHandlers()
        {
            GetLicenseButton.Click += OnGetLicenseClick;
        }
        
        private void OnGetLicenseClick(object sender, RoutedEventArgs e)
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
            
            GetLicenseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}