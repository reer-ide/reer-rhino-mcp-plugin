using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReerRhinoMCPPlugin.UI.Controls
{
    /// <summary>
    /// Log viewer card control for displaying activity logs
    /// </summary>
    public partial class LogViewerCard : UserControl
    {
        public LogViewerCard()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
