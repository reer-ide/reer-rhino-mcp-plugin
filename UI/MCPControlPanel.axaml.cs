using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReerRhinoMCPPlugin.UI.ViewModels;
using Rhino;
using System;

namespace ReerRhinoMCPPlugin.UI
{
    public partial class MCPControlPanel : Window
    {
        public MCPControlPanel()
        {
            InitializeComponent();
        }

        public MCPControlPanel(rhino_mcp_plugin.ReerRhinoMCPPlugin plugin)
        {
            try
            {
                InitializeComponent();
                DataContext = new MCPControlPanelViewModel(plugin);
                RhinoApp.WriteLine("[DEBUG] MCPControlPanel DataContext set successfully");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("[ERROR] MCPControlPanel ctor exception: " + ex);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // Clean up resources when window is closed
            base.OnClosed(e);
        }
    }
} 