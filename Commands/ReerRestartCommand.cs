using System;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Core.Client;

namespace ReerRhinoMCPPlugin.Commands
{
    public class ReerRestartCommand : Command
    {
        public ReerRestartCommand() { Instance = this; }
        public static ReerRestartCommand Instance { get; private set; }
        public override string EnglishName => "ReerRestart";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;
            var settings = ReerRhinoMCPPlugin.Instance.MCPSettings;

            Task.Run(async () =>
            {
                try
                {
                    RhinoApp.WriteLine("=== Restarting Connection (Fresh Session) ===");

                    if (connectionManager.IsConnected)
                    {
                        RhinoApp.WriteLine("Stopping current connection...");
                        // Force clean session info to start fresh
                        await connectionManager.StopConnectionAsync(cleanSessionInfo: true);
                        RhinoApp.WriteLine("✓ Connection stopped and session info cleaned");
                    }
                    else
                    {
                        RhinoApp.WriteLine("No active connection found");
                    }

                    // Clear any stored session info for fresh start
                    if (settings.DefaultConnection?.Mode == ConnectionMode.Remote)
                    {
                        try
                        {
                            var fileIntegrityManager = new FileIntegrityManager();
                            var licenseManager = new LicenseManager();
                            var licenseInfo = await licenseManager.GetStoredLicenseInfoAsync();
                            
                            if (licenseInfo != null)
                            {
                                RhinoApp.WriteLine("Clearing any stored session data...");
                                // Clear any cached session info
                                await fileIntegrityManager.ClearAllSessionsAsync();
                                RhinoApp.WriteLine("✓ Session data cleared");
                            }
                        }
                        catch (Exception ex)
                        {
                            RhinoApp.WriteLine($"Warning: Could not clear session data: {ex.Message}");
                        }
                    }

                    // Restart with current settings if valid
                    if (settings.IsValid())
                    {
                        RhinoApp.WriteLine("Restarting connection with fresh session...");
                        
                        var connectionSettings = settings.GetDefaultConnectionSettings();
                        bool success = await connectionManager.StartConnectionAsync(connectionSettings);
                        
                        if (success)
                        {
                            RhinoApp.WriteLine($"✓ {connectionSettings.Mode} connection restarted successfully with fresh session");
                        }
                        else
                        {
                            RhinoApp.WriteLine($"✗ Failed to restart {connectionSettings.Mode} connection");
                        }
                    }
                    else
                    {
                        RhinoApp.WriteLine("No valid connection settings found. Use 'ReerStart' to configure a new connection.");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error restarting connection: {ex.Message}");
                }
            });
            
            return Result.Success;
        }
    }
}