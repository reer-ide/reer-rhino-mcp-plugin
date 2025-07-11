using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReerRhinoMCPPlugin.UI.Controls
{
    /// <summary>
    /// UserControl for server control operations
    /// </summary>
    public partial class ServerControlCard : UserControl
    {
        public ServerControlCard()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
