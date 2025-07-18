using System;
using Rhino;
using Rhino.Commands;

namespace ReerRhinoMCPPlugin.Commands
{
    public class RhinoMCPServerCommand : Command
    {
        public RhinoMCPServerCommand()
        {
            Instance = this;
        }

        public static RhinoMCPServerCommand Instance { get; private set; }

        public override string EnglishName => "ReerRhinoMCP";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            RhinoApp.WriteLine("=== REER Rhino MCP Plugin ===");
            RhinoApp.WriteLine("This plugin connects Rhino to external applications using the Model Context Protocol.");
            RhinoApp.WriteLine("");
            RhinoApp.WriteLine("Available commands:");
            RhinoApp.WriteLine("  - ReerLicense: Manage your software license.");
            RhinoApp.WriteLine("  - ReerStart: Start a local or remote connection.");
            RhinoApp.WriteLine("  - ReerStop: Stop the active connection.");
            RhinoApp.WriteLine("  - ReerRestart: Stop and start a new connection.");
            RhinoApp.WriteLine("  - ReerUtils: Access utility tools for status and file management.");
            RhinoApp.WriteLine("");
            RhinoApp.WriteLine("For more detailed instructions, please refer to the documentation.");

            return Result.Success;
        }
    }
} 