using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReerRhinoMCPPlugin.UI.Controls
{
    /// <summary>
    /// UserControl for displaying connection status information
    /// </summary>
    public partial class ConnectionStatusCard : UserControl
    {
        public ConnectionStatusCard()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
