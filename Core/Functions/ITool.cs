using Newtonsoft.Json.Linq;

namespace ReerRhinoMCPPlugin.Core
{
    /// <summary>
    /// Interface that all MCP tools must implement
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Execute the tool with the provided parameters
        /// </summary>
        /// <param name="parameters">The parameters for the tool execution</param>
        /// <returns>The result of the tool execution</returns>
        JObject Execute(JObject parameters);
    }
} 