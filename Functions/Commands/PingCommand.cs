using Newtonsoft.Json.Linq;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Simple ping command for testing connection
    /// </summary>
    [MCPCommand("ping", "Test connection", RequiresDocument = false)]
    public class PingCommand : ICommand
    {
        public JObject Execute(JObject parameters)
        {
            return new JObject { ["message"] = "pong" };
        }
    }
} 