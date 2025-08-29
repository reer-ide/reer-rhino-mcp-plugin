using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("delete_rhino_layers", "Delete layers from Rhino document", ModifiesDocument = true)]
    public class DeleteRhinoLayers : ITool
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;

                // Handle both single layer and multiple layers input
                JArray layersToDelete = null;
                
                // Check if we have a "layers" array (new format)
                if (parameters["layers"] is JArray layersArray)
                {
                    layersToDelete = layersArray;
                }
                // Fall back to single layer format for backward compatibility
                else if (parameters["name"] != null || parameters["guid"] != null)
                {
                    layersToDelete = new JArray { parameters };
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "No layer identifiers provided (name or guid required)"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Delete Rhino Layers");

                try
                {
                    var deletedLayers = new JArray();
                    var results = new JObject();

                    foreach (var layerToken in layersToDelete)
                    {
                        var layerParams = layerToken as JObject;
                        if (layerParams == null) continue;

                        try
                        {
                            var result = DeleteSingleLayer(doc, layerParams);
                            deletedLayers.Add(result);
                            
                            // Add to results with layer name as key
                            string layerName = result["name"]?.ToString() ?? $"Layer_{deletedLayers.Count}";
                            results[layerName] = result;
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new JObject
                            {
                                ["status"] = "error",
                                ["error"] = ex.Message,
                                ["name"] = layerParams["name"]?.ToString() ?? layerParams["guid"]?.ToString() ?? "unknown"
                            };
                            deletedLayers.Add(errorResult);
                            
                            string layerName = layerParams["name"]?.ToString() ?? layerParams["guid"]?.ToString() ?? $"Error_{deletedLayers.Count}";
                            results[layerName] = errorResult;
                        }
                    }

                    // Update views
                    doc.Views.Redraw();

                    // End undo record
                    doc.EndUndoRecord(undoRecordSerialNumber);

                    return new JObject
                    {
                        ["status"] = "success",
                        ["layers_deleted"] = deletedLayers,
                        ["count"] = deletedLayers.Count,
                        ["results"] = results
                    };
                }
                catch (Exception)
                {
                    // End undo record and undo changes
                    doc.EndUndoRecord(undoRecordSerialNumber);
                    doc.Undo();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error deleting layers: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error deleting layers: {ex.Message}"
                };
            }
        }

        private JObject DeleteSingleLayer(RhinoDoc doc, JObject layerParams)
        {
            // Parse parameters
            bool hasName = layerParams.ContainsKey("name");
            bool hasGuid = layerParams.ContainsKey("guid");
            bool quietDelete = layerParams["quiet"]?.Value<bool>() ?? true; // Default to quiet delete

            string name = hasName ? layerParams["name"]?.ToString() : null;
            string guidStr = hasGuid ? layerParams["guid"]?.ToString() : null;

            if (!hasName && !hasGuid)
            {
                throw new InvalidOperationException("Either 'name' or 'guid' must be provided");
            }

            // Find the layer
            Layer layer = null;
            if (hasName && !string.IsNullOrEmpty(name))
            {
                layer = doc.Layers.FindName(name);
            }
            if (hasGuid && !string.IsNullOrEmpty(guidStr))
            {
                if (Guid.TryParse(guidStr, out Guid layerGuid))
                {
                    layer = doc.Layers.FindId(layerGuid);
                }
                else
                {
                    throw new ArgumentException($"Invalid GUID format: {guidStr}");
                }
            }

            if (layer == null)
            {
                throw new InvalidOperationException("Layer not found");
            }

            // Store layer info before deletion
            string layerName = layer.Name;
            Guid layerId = layer.Id;
            int layerIndex = layer.Index;

            // Check if layer can be deleted
            if (layer.Index == 0) // Default layer
            {
                throw new InvalidOperationException("Cannot delete the default layer (layer 0)");
            }

            // Check if layer has child layers
            var childLayers = doc.Layers.Where(l => l.ParentLayerId == layer.Id).ToList();
            if (childLayers.Any())
            {
                var childNames = string.Join(", ", childLayers.Select(l => l.Name));
                throw new InvalidOperationException($"Cannot delete layer '{layerName}' because it has child layers: {childNames}");
            }

            // Check if layer has objects (unless force delete is specified)
            bool forceDelete = layerParams["force"]?.Value<bool>() ?? false;
            var objectsOnLayer = doc.Objects.FindByLayer(layer).ToList();
            if (objectsOnLayer.Any() && !forceDelete)
            {
                throw new InvalidOperationException($"Layer '{layerName}' contains {objectsOnLayer.Count} objects. Use 'force: true' to delete anyway.");
            }

            // If force delete and has objects, delete the objects first
            if (forceDelete && objectsOnLayer.Any())
            {
                foreach (var obj in objectsOnLayer)
                {
                    doc.Objects.Delete(obj.Id, quietDelete);
                }
            }

            // Delete the layer
            bool success = doc.Layers.Delete(layerIndex, quietDelete);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to delete layer '{layerName}'");
            }

            return new JObject
            {
                ["status"] = "success",
                ["name"] = layerName,
                ["id"] = layerId.ToString(),
                ["objects_deleted"] = forceDelete ? objectsOnLayer.Count : 0,
                ["message"] = $"Layer '{layerName}' deleted successfully"
            };
        }
    }
}
