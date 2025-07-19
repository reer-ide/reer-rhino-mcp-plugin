using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core
{
    public class ToolExecutor
    {
        private readonly Dictionary<string, (ITool toolInstance, MCPToolAttribute attr)> _tools;

            public ToolExecutor()
        {
            _tools = DiscoverTools();
            RhinoApp.WriteLine($"ToolExecutor initialized with {_tools.Count} tools.");
        }

        public string ProcessTool(JObject tool, string clientId)
        {
            try
            {
                string toolType = tool["type"]?.ToString();
                if (string.IsNullOrEmpty(toolType))
                {
                    return CreateErrorResponse("Tool type is required").ToString();
                }

                var parameters = tool["params"] as JObject ?? new JObject();

                if (!_tools.ContainsKey(toolType))
                {
                    return CreateErrorResponse($"Unknown tool: {toolType}").ToString();
                }

                var result = ExecuteTool(toolType, parameters);
                
                // Return the raw tool result directly without extra wrapping
                // The RhinoMCPClient will handle the response structure
                return result.ToString();
            }
            catch (Exception ex)
            {
                return CreateErrorResponse(ex.Message).ToString();
            }
        }

        private Dictionary<string, (ITool toolInstance, MCPToolAttribute attr)> DiscoverTools()
        {
            var tools = new Dictionary<string, (ITool, MCPToolAttribute)>();
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var toolTypes = assembly.GetTypes()
                    .Where(type => type.IsClass && !type.IsAbstract && typeof(ITool).IsAssignableFrom(type))
                    .ToList();

                foreach (var toolType in toolTypes)
                {
                    var attr = toolType.GetCustomAttribute<MCPToolAttribute>();
                    if (attr != null)
                    {
                        var toolInstance = (ITool)Activator.CreateInstance(toolType);
                        tools[attr.ToolName] = (toolInstance, attr);
                        // RhinoApp.WriteLine($"Registered tool: {attr.ToolName}");
                    }
                }
                RhinoApp.WriteLine($"ReerRhinoMCPPlugin: Registered {tools.Count} tools");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error discovering tools: {ex.Message}");
            }
            return tools;
        }

        private JObject ExecuteTool(string toolType, JObject parameters)
        {
            try
            {
                if (!_tools.ContainsKey(toolType))
                {
                    throw new InvalidOperationException($"Tool '{toolType}' not found");
                }

                var (toolInstance, toolAttribute) = _tools[toolType];

                // Check if tool requires document
                if (toolAttribute.RequiresDocument)
                {
                    var doc = RhinoDoc.ActiveDoc;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("This tool requires an active Rhino document");
                    }
                }

                // Execute the tool
                return toolInstance.Execute(parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing tool '{toolType}': {ex.Message}", ex);
            }
        }

        private JObject CreateErrorResponse(string message) => new JObject { ["status"] = "error", ["message"] = message };
    }
} 