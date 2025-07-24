using System;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin;

namespace ReerRhinoMCPPlugin.Commands
{
    public class ReerStopCommand : Command
    {
        public ReerStopCommand() { Instance = this; }
        public static ReerStopCommand Instance { get; private set; }
        public override string EnglishName => "ReerStop";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var connectionManager = ReerRhinoMCPPlugin.Instance.ConnectionManager;

            Task.Run(async () =>
            {
                try
                {
                    RhinoApp.WriteLine("=== Stopping Connection ===");

                    if (!connectionManager.IsConnected)
                    {
                        RhinoApp.WriteLine("No active connection to stop.");
                        return;
                    }

                    // For remote connections, preserve session info for automatic reconnection
                    bool cleanSessionInfo = connectionManager.ActiveConnection?.Settings?.Mode != ConnectionMode.Remote;
                    await connectionManager.StopConnectionAsync(cleanSessionInfo);

                    if (cleanSessionInfo)
                    {
                        RhinoApp.WriteLine("✓ Connection stopped successfully.");
                    }
                    else
                    {
                        RhinoApp.WriteLine("✓ Connection stopped successfully. Session info preserved for automatic reconnection.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error stopping connection: {ex.Message}");
                }
            });
            
            return Result.Success;
        }
    }
}
