using System;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Functions
{
    /// <summary>
    /// Basic command handler for MCP protocol commands
    /// </summary>
    public class BasicCommandHandler
    {
        /// <summary>
        /// Processes an MCP command and returns a response
        /// </summary>
        /// <param name="command">The command to process</param>
        /// <param name="clientId">ID of the client that sent the command</param>
        /// <returns>JSON response string</returns>
        public string ProcessCommand(JObject command, string clientId)
        {
            try
            {
                string commandType = command["type"]?.ToString();
                JObject parameters = command["params"] as JObject ?? new JObject();

                RhinoApp.WriteLine($"Processing command '{commandType}' from client {clientId}");

                JObject result = ExecuteCommand(commandType, parameters);

                return result.ToString();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing command: {ex.Message}");
                return CreateErrorResponse(ex.Message).ToString();
            }
        }

        private JObject ExecuteCommand(string commandType, JObject parameters)
        {
            switch (commandType?.ToLowerInvariant())
            {
                case "ping":
                    return CreateSuccessResponse(new JObject { ["message"] = "pong" });

                case "get_rhino_info":
                    return CreateSuccessResponse(GetRhinoInfo());

                case "get_document_info":
                    return CreateSuccessResponse(GetDocumentInfo());

                default:
                    return CreateErrorResponse($"Unknown command type: {commandType}");
            }
        }

        private JObject GetRhinoInfo()
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
                RhinoApp.WriteLine($"Error getting Rhino info: {ex.Message}");
                return new JObject { ["error"] = ex.Message };
            }
        }

        private JObject GetDocumentInfo()
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                {
                    return new JObject { ["error"] = "No active document" };
                }

                return new JObject
                {
                    ["document_name"] = doc.Name ?? "Untitled",
                    ["path"] = doc.Path ?? "",
                    ["modified"] = doc.Modified,
                    ["object_count"] = doc.Objects.Count,
                    ["layer_count"] = doc.Layers.Count,
                    ["units"] = doc.ModelUnitSystem.ToString()
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting document info: {ex.Message}");
                return new JObject { ["error"] = ex.Message };
            }
        }

        private JObject CreateSuccessResponse(JObject result)
        {
            return new JObject
            {
                ["status"] = "success",
                ["result"] = result
            };
        }

        private JObject CreateErrorResponse(string message)
        {
            return new JObject
            {
                ["status"] = "error",
                ["message"] = message
            };
        }
    }
} 