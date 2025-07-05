using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReerRhinoMCPPlugin.UI.ViewModels;
using Rhino;
using System;

namespace ReerRhinoMCPPlugin.UI.Windows
{
    /// <summary>
    /// Simplified main control panel window
    /// </summary>
    public partial class MCPControlPanelNew : Window
    {
        private MCPControlPanelViewModel _viewModel;

        public MCPControlPanelNew()
        {
            InitializeComponent();
        }

        public MCPControlPanelNew(ReerRhinoMCPPlugin plugin)
        {
            try
            {
                InitializeComponent();
                
                _viewModel = new MCPControlPanelViewModel(plugin);
                DataContext = _viewModel;
                
                // Subscribe to KeepOnTop changes
                _viewModel.KeepOnTopChanged += OnKeepOnTopChanged;
                
                // Window event handlers
                this.Opened += OnWindowOpened;
                
                RhinoApp.WriteLine("[DEBUG] MCPControlPanelNew initialized successfully");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] MCPControlPanelNew constructor exception: {ex}");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnWindowOpened(object sender, EventArgs e)
        {
            try
            {
                this.WindowState = WindowState.Normal;
                this.Activate();
                UpdateTopmost();
                this.Focus();
                RhinoApp.WriteLine("[DEBUG] Window opened and activated");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Error in OnWindowOpened: {ex.Message}");
            }
        }

        private void OnKeepOnTopChanged(object sender, bool keepOnTop)
        {
            try
            {
                UpdateTopmost();
                RhinoApp.WriteLine($"[DEBUG] KeepOnTop changed to: {keepOnTop}");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Error in OnKeepOnTopChanged: {ex.Message}");
            }
        }

        private void UpdateTopmost()
        {
            try
            {
                if (_viewModel != null)
                {
                    this.Topmost = _viewModel.KeepOnTop;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Error updating topmost: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _viewModel?.Dispose();
                RhinoApp.WriteLine("[DEBUG] MCPControlPanelNew closed and disposed");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[ERROR] Error in OnClosed: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}
