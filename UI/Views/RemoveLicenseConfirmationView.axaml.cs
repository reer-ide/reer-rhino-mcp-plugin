using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ReerRhinoMCPPlugin.UI.Views
{
    public partial class RemoveLicenseConfirmationView : UserControl
    {
        public event EventHandler<RemoveLicenseEventArgs> RemoveLicenseRequested;
        
        private readonly ReerRhinoMCPPlugin _plugin;
        
        public RemoveLicenseConfirmationView()
        {
            InitializeComponent();
        }
        
        public RemoveLicenseConfirmationView(ReerRhinoMCPPlugin plugin) : this()
        {
            _plugin = plugin;
            SetupEventHandlers();
        }
        
        private void SetupEventHandlers()
        {
            RemoveButton.Click += OnRemoveClick;
            CancelButton.Click += OnCancelClick;
        }
        
        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            // Clear the license
            _plugin.LicenseManager.ClearStoredLicense();
            
            // Notify parent to switch to registration view
            RemoveLicenseRequested?.Invoke(this, new RemoveLicenseEventArgs { Action = RemoveLicenseAction.Removed });
        }
        
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            // Notify parent to go back to management view
            RemoveLicenseRequested?.Invoke(this, new RemoveLicenseEventArgs { Action = RemoveLicenseAction.Cancelled });
        }
    }
    
    public enum RemoveLicenseAction
    {
        Removed,
        Cancelled
    }
    
    public class RemoveLicenseEventArgs : EventArgs
    {
        public RemoveLicenseAction Action { get; set; }
    }
}