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
                JObject parameters = tool["params"] as JObject ?? new JObject();
                RhinoApp.WriteLine($"Executing tool '{toolType}' from client {clientId}");
                JObject result = ExecuteTool(toolType, parameters);
                return result.ToString();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing tool: {ex.Message}");
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
                        RhinoApp.WriteLine($"Registered tool: {attr.ToolName}");
                    }
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error discovering tools: {ex.Message}");
            }
            return tools;
        }

        private JObject ExecuteTool(string toolType, JObject parameters)
        {
            if (string.IsNullOrEmpty(toolType) || !_tools.TryGetValue(toolType, out var tool))
            {
                return CreateErrorResponse($"Unknown tool type: {toolType}");
            }

            var (toolInstance, attr) = tool;
            if (attr.RequiresDocument && RhinoDoc.ActiveDoc == null)
            {
                    return CreateErrorResponse("Tool requires an active Rhino document.");
            }

            try
            {
                if (attr.ModifiesDocument && RhinoDoc.ActiveDoc != null)
                {
                    var doc = RhinoDoc.ActiveDoc;
                    var undoRecord = doc.BeginUndoRecord($"MCP Tool: {toolType}");
                    var result = toolInstance.Execute(parameters);
                    doc.EndUndoRecord(undoRecord);
                    return CreateSuccessResponse(result);
                }
                else
                {
                    var result = toolInstance.Execute(parameters);
                    return CreateSuccessResponse(result);
                }
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Error executing '{toolType}': {ex.Message}");
            }
        }

        private JObject CreateSuccessResponse(JObject result) => new JObject { ["status"] = "success", ["result"] = result };
        private JObject CreateErrorResponse(string message) => new JObject { ["status"] = "error", ["message"] = message };
    }
} 