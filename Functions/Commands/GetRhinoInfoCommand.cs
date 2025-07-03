using System;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Command to get Rhino version and plugin information
    /// </summary>
    [MCPCommand("get_rhino_info", "Get Rhino version and plugin info", RequiresDocument = false)]
    public class GetRhinoInfoCommand : ICommand
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                return new JObject
                {
                    ["rhino_version"] = RhinoApp.Version.ToString(),
                    ["build_date"] = RhinoApp.BuildDate.ToString("yyyy-MM-dd"),
                    ["plugin_name"] = "REER Rhino MCP Plugin",
                    ["plugin_version"] = "1.0.0"
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting Rhino info: {ex.Message}");
            }
        }
    }
} 