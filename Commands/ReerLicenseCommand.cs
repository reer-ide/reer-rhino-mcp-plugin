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
            
            try
            {
                // Show menu on main thread
                var option = ShowLicenseMenu();
                
                // Handle each option appropriately
                switch (option)
                {
                    case LicenseMenuOption.Register:
                        return HandleRegisterLicense();
                    case LicenseMenuOption.Check:
                        HandleCheckLicense();
                        break;
                    case LicenseMenuOption.Clear:
                        HandleClearLicense();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred: {ex.Message}");
                return Result.Failure;
            }

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

        private Result HandleRegisterLicense()
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Registration ===");
                
                // Collect all inputs first before proceeding
                var licenseKey = GetUserInput("Enter your license key:");
                if (string.IsNullOrEmpty(licenseKey)) 
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }

                var userId = GetUserInput("Enter your user ID:");
                if (string.IsNullOrEmpty(userId)) 
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }

                var serverUrl = GetUserInput("Enter remote MCP server URL:", "http://127.0.0.1:8080");
                if (string.IsNullOrEmpty(serverUrl)) 
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }
                
                // Confirm all inputs before proceeding
                RhinoApp.WriteLine("\n=== Registration Details ===");
                RhinoApp.WriteLine($"License Key: {licenseKey.Substring(0, Math.Min(8, licenseKey.Length))}...");
                RhinoApp.WriteLine($"User ID: {userId}");
                RhinoApp.WriteLine($"Server URL: {serverUrl}");
                
                var confirm = GetUserInput("Proceed with registration? (yes/no):", "yes");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("License registration cancelled.");
                    return Result.Cancel;
                }
                
                RhinoApp.WriteLine("Registering license... (this may take a few seconds)");
                
                // Run async operation and wait for completion
                Task.Run(async () =>
                {
                    try
                    {
                        var remoteClient = new RhinoMCPClient();
                        var success = await remoteClient.RegisterLicenseAsync(licenseKey, userId, serverUrl);

                        if (success)
                        {
                            RhinoApp.WriteLine("\n[OK] License registration completed successfully!");
                            RhinoApp.WriteLine("You can now use 'ReerStart' to connect.");
                        }
                        else
                        {
                            RhinoApp.WriteLine("\n[ERR] License registration failed. Please check your details and try again.");
                        }
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Error during license registration: {ex.Message}");
                    }
                });
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during license registration: {ex.Message}");
                return Result.Failure;
            }
        }

        private void HandleCheckLicense()
        {
            try
            {
                RhinoApp.WriteLine("=== RhinoMCP License Status ===");
                
                Task.Run(async () =>
                {
                    try
                    {
                        var remoteClient = new RhinoMCPClient();
                        var licenseResult = await remoteClient.GetLicenseStatusAsync();

                        if (licenseResult.IsValid)
                        {
                            RhinoApp.WriteLine("[OK] License is valid and active");
                            RhinoApp.WriteLine($"  License ID: {licenseResult.LicenseId}");
                            RhinoApp.WriteLine($"  User ID: {licenseResult.UserId}");
                            RhinoApp.WriteLine($"  Tier: {licenseResult.Tier}");
                            RhinoApp.WriteLine($"  Max concurrent files: {licenseResult.MaxConcurrentFiles}");
                            var displayFingerprint = MachineFingerprinting.GetDisplayFingerprint();
                            RhinoApp.WriteLine($"  Machine fingerprint: {displayFingerprint}");
                        }
                        else
                        {
                            RhinoApp.WriteLine("[ERR] License validation failed");
                            RhinoApp.WriteLine($"  Reason: {licenseResult.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error checking license: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking license: {ex.Message}");
            }
        }

        private void HandleClearLicense()
        {
            try
            {
                RhinoApp.WriteLine("=== Clear Stored License ===");
                var confirm = GetUserInput("Are you sure? (yes/no):", "no");
                if (confirm?.ToLower() != "yes")
                {
                    RhinoApp.WriteLine("License clear cancelled.");
                    return;
                }
                
                var remoteClient = new RhinoMCPClient();
                remoteClient.ClearStoredLicense();
                RhinoApp.WriteLine("[OK] Stored license cleared successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error clearing license: {ex.Message}");
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
