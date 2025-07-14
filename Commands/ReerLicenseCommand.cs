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
    public class ReerLicenseCommand : Command
    {
        public ReerLicenseCommand() { Instance = this; }
        public static ReerLicenseCommand Instance { get; private set; }
        public override string EnglishName => "ReerLicense";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;
            
            Task.Run(async () => 
            {
                try
                {
                    var option = ShowLicenseMenu();
                    switch (option)
                    {
                        case LicenseMenuOption.Register:
                            await RunRegisterLicense(connectionManager);
                            break;
                        case LicenseMenuOption.Check:
                            await RunCheckLicense(connectionManager);
                            break;
                        case LicenseMenuOption.Clear:
                            await RunClearLicense(connectionManager);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"An error occurred: {ex.Message}");
                }
            });

            return Result.Success;
        }

        private LicenseMenuOption ShowLicenseMenu()
        {
            var getter = new GetOption();
            getter.SetCommandPrompt("RhinoMCP License: Select an option");
            getter.AddOption("Register", "Register a new license with the remote server");
            getter.AddOption("Check", "Check current license status");
            getter.AddOption("Clear", "Clear stored license (for troubleshooting)");
            getter.AddOption("Cancel", "Return to main menu");

            var result = getter.Get();

            if (result != GetResult.Option)
                return LicenseMenuOption.Cancel;

            var selectedOption = getter.Option().EnglishName;
            switch (selectedOption)
            {
                case "Register": return LicenseMenuOption.Register;
                case "Check": return LicenseMenuOption.Check;
                case "Clear": return LicenseMenuOption.Clear;
                default: return LicenseMenuOption.Cancel;
            }
        }

        private async Task<bool> RunRegisterLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Registration ===");
                var licenseKey = GetUserInput("Enter your license key:");
                if (string.IsNullOrEmpty(licenseKey)) return true;

                var userId = GetUserInput("Enter your user ID:");
                if (string.IsNullOrEmpty(userId)) return true;

                var serverUrl = GetUserInput("Enter remote MCP server URL:", "https://rhinomcp.your-server.com");
                if (string.IsNullOrEmpty(serverUrl)) return true;
                
                RhinoApp.WriteLine("Registering license... (this may take a few seconds)");
                
                var remoteClient = new RhinoMCPClient();
                var success = await remoteClient.RegisterLicenseAsync(licenseKey, userId, serverUrl);

                if (success)
                {
                    RhinoApp.WriteLine("\n✓ License registration completed successfully!");
                    RhinoApp.WriteLine("You can now use 'StartRemote' to connect.");
                }
                else
                {
                    RhinoApp.WriteLine("\n✗ License registration failed. Please check your details and try again.");
                }
                return success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error during license registration: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunCheckLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Status ===");
                var remoteClient = new RhinoMCPClient();
                var licenseResult = await remoteClient.GetLicenseStatusAsync();

                if (licenseResult.IsValid)
                {
                    RhinoApp.WriteLine("✓ License is valid and active");
                    RhinoApp.WriteLine($"  License ID: {licenseResult.LicenseId}");
                    RhinoApp.WriteLine($"  User ID: {licenseResult.UserId}");
                    RhinoApp.WriteLine($"  Tier: {licenseResult.Tier}");
                    RhinoApp.WriteLine($"  Max concurrent files: {licenseResult.MaxConcurrentFiles}");
                    var displayFingerprint = MachineFingerprinting.GetDisplayFingerprint();
                    RhinoApp.WriteLine($"  Machine fingerprint: {displayFingerprint}");
                }
                else
                {
                    RhinoApp.WriteLine("✗ License validation failed");
                    RhinoApp.WriteLine($"  Reason: {licenseResult.Message}");
                }
                return true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error checking license: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> RunClearLicense(IConnectionManager connectionManager)
        {
            try
            {
                RhinoApp.WriteLine("=== Clear Stored License ===");
                var confirm = GetUserInput("Are you sure? (yes/no):", "no");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("License clear cancelled.");
                    return true;
                }
                
                var remoteClient = new RhinoMCPClient();
                remoteClient.ClearStoredLicense();
                RhinoApp.WriteLine("✓ Stored license cleared successfully.");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error clearing license: {ex.Message}");
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

        private enum LicenseMenuOption { Register, Check, Clear, Cancel }
    }
}
