using System;
using System.Drawing;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Serializers;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("create_rhino_layers", "Create layers in Rhino document", ModifiesDocument = true)]
    public class CreateRhinoLayers : ITool
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                // Handle both single layer and multiple layers input
                JArray layersToCreate = null;
                
                // Check if we have a "layers" array (new format)
                if (parameters["layers"] is JArray layersArray)
                {
                    layersToCreate = layersArray;
                }
                // Fall back to single layer format for backward compatibility
                else if (parameters["name"] != null || parameters.Count > 0)
                {
                    layersToCreate = new JArray { parameters };
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "No layer parameters provided"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Create Rhino Layers");

                try
                {
                    var createdLayers = new JArray();
                    var results = new JObject();

                    foreach (var layerToken in layersToCreate)
                    {
                        var layerParams = layerToken as JObject;
                        if (layerParams == null) continue;

                        try
                        {
                            var result = CreateSingleLayer(doc, layerParams);
                            createdLayers.Add(result);
                            
                            // Add to results with layer name as key
                            string layerName = result["name"]?.ToString() ?? $"Layer_{createdLayers.Count}";
                            results[layerName] = result;
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new JObject
                            {
                                ["status"] = "error",
                                ["error"] = ex.Message,
                                ["name"] = layerParams["name"]?.ToString() ?? "unknown"
                            };
                            createdLayers.Add(errorResult);
                            
                            string layerName = layerParams["name"]?.ToString() ?? $"Error_{createdLayers.Count}";
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
                        ["layers_created"] = createdLayers,
                        ["count"] = createdLayers.Count,
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
                Logger.Error($"Error creating layers: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error creating layers: {ex.Message}"
                };
            }
        }

        private JObject CreateSingleLayer(RhinoDoc doc, JObject layerParams)
        {
            // Parse parameters
            bool hasName = layerParams.ContainsKey("name");
            bool hasColor = layerParams.ContainsKey("color");
            bool hasParent = layerParams.ContainsKey("parent");

            string name = hasName ? layerParams["name"]?.ToString() : null;
            int[] color = hasColor ? ParameterUtils.GetValidatedColorFromToken(layerParams["color"]) : null;
            string parent = hasParent ? layerParams["parent"]?.ToString() : null;

            // Create new layer
            var layer = new Layer();
            
            if (hasName)
            {
                layer.Name = name;
            }
            
            if (hasColor && color != null)
            {
                layer.Color = Color.FromArgb(color[0], color[1], color[2]);
            }

            if (hasParent)
            {
                var parentLayer = doc.Layers.FindName(parent);
                if (parentLayer != null)
                {
                    layer.ParentLayerId = parentLayer.Id;
                }
                else
                {
                    throw new InvalidOperationException($"Parent layer '{parent}' not found");
                }
            }

            // Add layer to document
            var layerId = doc.Layers.Add(layer);
            if (layerId < 0)
            {
                throw new InvalidOperationException("Failed to add layer to document");
            }

            // Get the actual created layer
            var createdLayer = doc.Layers.FindIndex(layerId);
            if (createdLayer == null)
            {
                throw new InvalidOperationException("Failed to retrieve created layer");
            }

            // Return serialized layer info
            var result = Serializer.SerializeLayer(createdLayer);
            result["status"] = "success";
            
            return result;
        }


    }
}
