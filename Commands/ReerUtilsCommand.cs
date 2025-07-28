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
            var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;

            Task.Run(async () =>
            {
                try
                {
                    var option = ShowToolsMenu();
                    switch (option)
                    {
                        case ToolsMenuOption.Status:
                            await RunStatus(connectionManager);
                            break;
                        case ToolsMenuOption.CheckFiles:
                            await RunCheckFiles(connectionManager);
                            break;
                        case ToolsMenuOption.ClearFiles:
                            await RunClearFiles(connectionManager);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"An error occurred: {ex.Message}");
                }
            });
            
            return Result.Success;
        }

        private ToolsMenuOption ShowToolsMenu()
        {
            var getter = new GetOption();
            getter.SetCommandPrompt("RhinoMCP Utilities: Select a tool");
            getter.AddOption("Status", "Show connection and license status");
            getter.AddOption("CheckFiles", "Check status of linked files");
            getter.AddOption("ClearFiles", "Clear all linked files (troubleshooting)");
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
                default: return ToolsMenuOption.Cancel;
            }
        }

        private async Task<bool> RunStatus(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP Status ===");
                RhinoApp.WriteLine($"Connection Status: {connectionManager.Status}");
                
                var remoteClient = new RhinoMCPClient();
                var licenseResult = await remoteClient.GetLicenseStatusAsync();

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
                var remoteClient = new RhinoMCPClient();
                var changes = await remoteClient.ValidateLinkedFilesAsync();
                
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

                var remoteClient = new RhinoMCPClient();
                await remoteClient.ClearLinkedFilesAsync();
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

        private enum ToolsMenuOption { Status, CheckFiles, ClearFiles, Cancel }
    }
} 