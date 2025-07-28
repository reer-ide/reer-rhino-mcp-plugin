using System;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    /// <summary>
    /// Attribute to mark classes as MCP tool handlers
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MCPToolAttribute : Attribute
    {
        /// <summary>
        /// The tool name that triggers this handler
        /// </summary>
        public string ToolName { get; }
        
        /// <summary>
        /// Description of what this tool does
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Whether this tool requires an active Rhino document
        /// </summary>
        public bool RequiresDocument { get; set; } = true;
        
        /// <summary>
        /// Whether this tool can modify the document (affects undo recording)
        /// </summary>
        public bool ModifiesDocument { get; set; } = false;
        
        public MCPToolAttribute(string toolName, string description = "")
        {
            ToolName = toolName;
            Description = description;
        }   
    }
} 