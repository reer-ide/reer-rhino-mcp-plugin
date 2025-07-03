using System;

namespace ReerRhinoMCPPlugin.Functions
{
    /// <summary>
    /// Attribute to mark classes as MCP command handlers
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MCPCommandAttribute : Attribute
    {
        /// <summary>
        /// The command name that triggers this handler
        /// </summary>
        public string CommandName { get; }
        
        /// <summary>
        /// Description of what this command does
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Whether this command requires an active Rhino document
        /// </summary>
        public bool RequiresDocument { get; set; } = true;
        
        /// <summary>
        /// Whether this command can modify the document (affects undo recording)
        /// </summary>
        public bool ModifiesDocument { get; set; } = false;
        
        public MCPCommandAttribute(string commandName, string description = "")
        {
            CommandName = commandName;
            Description = description;
        }
    }
} 