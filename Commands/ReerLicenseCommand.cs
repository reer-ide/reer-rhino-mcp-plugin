using System;
using Rhino;
using Rhino.Commands;

namespace ReerRhinoMCPPlugin.Commands
{
    public class ReerLicenseUICommand : Command
    {
        public ReerLicenseUICommand() { Instance = this; }
        public static ReerLicenseUICommand Instance { get; private set; }
        public override string EnglishName => "ReerLicense";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                var plugin = ReerRhinoMCPPlugin.Instance;
                if (plugin == null)
                {
                    RhinoApp.WriteLine("MCP Plugin not loaded");
                    return Result.Failure;
                }

                plugin.ShowLicenseManagementUI();
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error opening License UI: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}