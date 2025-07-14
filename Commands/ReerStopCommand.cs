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

                    await connectionManager.StopConnectionAsync();

                    RhinoApp.WriteLine("✓ Connection stopped successfully.");
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error stopping connection: {ex.Message}");
                }
            });
            
            return Result.Success;
        }
    }
}
