using Newtonsoft.Json.Linq;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Interface for all MCP command implementations
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Execute the command with given parameters
        /// </summary>
        /// <param name="parameters">Command parameters</param>
        /// <returns>Command result as JObject</returns>
        JObject Execute(JObject parameters);
    }
} 