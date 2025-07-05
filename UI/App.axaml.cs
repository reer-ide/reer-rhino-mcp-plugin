using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ReerRhinoMCPPlugin.UI
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // This is called when the framework is initialized
            // For a plugin, we don't need to set up a main window here
            // as windows will be created on demand
            base.OnFrameworkInitializationCompleted();
        }
    }
} 