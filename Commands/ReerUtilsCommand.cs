using System;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Client;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin;

namespace ReerRhinoMCPPlugin.Commands
{
    public class ReerUtilsCommand : Command
    {
        public ReerUtilsCommand() { Instance = this; }
        public static ReerUtilsCommand Instance { get; private set; }
        public override string EnglishName => "ReerUtils";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;
                
                // Show menu and get user selection synchronously
                var option = ShowToolsMenu();
                if (option == ToolsMenuOption.Cancel)
                    return Result.Cancel;
                
                // Execute the selected function synchronously
                // Using .GetAwaiter().GetResult() for cross-platform compatibility
                switch (option)
                {
                    case ToolsMenuOption.Status:
                        var statusResult = RunStatus(connectionManager).ConfigureAwait(false).GetAwaiter().GetResult();
                        break;
                    case ToolsMenuOption.CheckFiles:
                        var checkResult = RunCheckFiles(connectionManager).ConfigureAwait(false).GetAwaiter().GetResult();
                        break;
                    case ToolsMenuOption.ClearFiles:
                        var clearResult = RunClearFiles(connectionManager).ConfigureAwait(false).GetAwaiter().GetResult();
                        break;
                    case ToolsMenuOption.ToggleDev:
                        var toggleResult = RunToggleDevelopmentMode().ConfigureAwait(false).GetAwaiter().GetResult();
                        break;
                    default:
                        return Result.Cancel;
                }
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred: {ex.Message}");
                RhinoApp.WriteLine($"Error: {ex.Message}");
                return Result.Failure;
            }
        }

        private ToolsMenuOption ShowToolsMenu()
        {
            var getter = new GetOption();
            getter.SetCommandPrompt("RhinoMCP Utilities: Select a tool");
            getter.AddOption("Status", "Show connection and license status");
            getter.AddOption("CheckFiles", "Check status of linked files");
            getter.AddOption("ClearFiles", "Clear all linked files (troubleshooting)");
            getter.AddOption("ToggleDev", "Toggle development/production mode");
            getter.AddOption("Cancel", "Return to main menu");
            
            var result = getter.Get();

            if (result != GetResult.Option)
                return ToolsMenuOption.Cancel;

            var selectedOption = getter.Option().EnglishName;
            switch (selectedOption)
            {
                case "Status": return ToolsMenuOption.Status;
                case "CheckFiles": return ToolsMenuOption.CheckFiles;
                case "ClearFiles": return ToolsMenuOption.ClearFiles;
                case "ToggleDev": return ToolsMenuOption.ToggleDev;
                default: return ToolsMenuOption.Cancel;
            }
        }

        private async Task<bool> RunStatus(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP Status ===");
                RhinoApp.WriteLine($"Connection Status: {connectionManager.Status}");
                
                var licenseResult = await ReerRhinoMCPPlugin.Instance.LicenseManager.GetLicenseStatusAsync();

                RhinoApp.WriteLine($"License Status: {(licenseResult.IsValid ? "Valid" : "Invalid")}");
                if (licenseResult.IsValid)
                {
                    RhinoApp.WriteLine($"  License ID: {licenseResult.LicenseId} (Tier: {licenseResult.Tier})");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting status: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunCheckFiles(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Checking Linked Files ===");
                var changes = await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.CheckLinkedFilesAsync();
                
                if (changes.Count == 0)
                {
                    RhinoApp.WriteLine("✓ All linked files are up to date.");
                }
                else
                {
                    RhinoApp.WriteLine($"Found {changes.Count} file status changes:");
                    foreach(var change in changes)
                    {
                        RhinoApp.WriteLine($"  - {change.Message}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking files: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunClearFiles(IConnectionManager connectionManager)
        {
            try
            {   
                RhinoApp.WriteLine("=== Clear Linked Files ===");
                 var confirm = GetUserInput("This will clear all linked file records. Are you sure? (yes/no):", "no");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("File clear cancelled.");
                    return true;
                }

                await ReerRhinoMCPPlugin.Instance.FileIntegrityManager.ClearAllLinkedFilesAsync();
                RhinoApp.WriteLine("✓ All linked files cleared successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing files: {ex.Message}");
                return false;
            }
        }

        private string GetUserInput(string prompt, string defaultValue = null)
        {
            var getter = new GetString();
            getter.SetCommandPrompt(prompt);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                getter.SetDefaultString(defaultValue);
            }
            return getter.Get() == GetResult.String ? getter.StringResult() : null;
        }

        private Task<bool> RunToggleDevelopmentMode()
        {
            try
            {
                var settings = ReerRhinoMCPPlugin.Instance.MCPSettings;
                var currentMode = settings.DevelopmentMode ? "Development" : "Production";
                var newMode = settings.DevelopmentMode ? "Production" : "Development";
                
                RhinoApp.WriteLine($"=== Toggle Development Mode ===");
                RhinoApp.WriteLine($"Current mode: {currentMode}");
                RhinoApp.WriteLine($"Current server: {ConnectionSettings.GetServerUrl()}");
                RhinoApp.WriteLine($"");
                RhinoApp.WriteLine($"Switch to {newMode} mode?");
                RhinoApp.WriteLine($"New server would be: {(settings.DevelopmentMode ? ConnectionSettings.PRODUCTION_SERVER_URL : ConnectionSettings.DEVELOPMENT_SERVER_URL)}");
                
                var confirm = GetUserInput("Proceed with mode change? (yes/no):", "no");
                if (confirm?.ToLower() == "yes")
                {
                    settings.DevelopmentMode = !settings.DevelopmentMode;
                    settings.Save();
                    
                    RhinoApp.WriteLine($"✓ Switched to {newMode} mode");
                    RhinoApp.WriteLine($"✓ Server URL: {ConnectionSettings.GetServerUrl()}");
                    RhinoApp.WriteLine("Note: You may need to restart any active connections for this change to take effect.");
                    return Task.FromResult(true);
                }
                else
                {
                    RhinoApp.WriteLine("Mode change cancelled.");
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error toggling development mode: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        private enum ToolsMenuOption { Status, CheckFiles, ClearFiles, ToggleDev, Cancel }
    }
} 