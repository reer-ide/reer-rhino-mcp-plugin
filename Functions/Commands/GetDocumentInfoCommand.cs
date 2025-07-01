using System;
using Newtonsoft.Json.Linq;
using Rhino;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Command to get active document information
    /// </summary>
    [MCPCommand("get_document_info", "Get active document information", RequiresDocument = true)]
    public class GetDocumentInfoCommand : ICommand
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                {
                    throw new Exception("No active document");
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
                throw new Exception($"Error getting document info: {ex.Message}");
            }
        }
    }
} 