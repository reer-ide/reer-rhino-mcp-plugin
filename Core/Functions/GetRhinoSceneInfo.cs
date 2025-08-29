using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Serializers;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("get_rhino_scene_info", "Get basic information about the current Rhino scene")]
    public class GetRhinoSceneInfo : ITool
    {
        public JObject Execute(JObject parameters)
        {
            const int SAMPLE_OBJECTS_PER_LAYER = 5;
            
            Logger.Info("Getting scene info...");

            var doc = RhinoDoc.ActiveDoc;

            var layersData = new JArray();
            var objectsByLayer = new Dictionary<int, List<RhinoObject>>();
            int totalObjectCount = 0;

            // Single pass through objects - group by layer and count active objects
            foreach (var obj in doc.Objects)
            {
                if (obj != null && obj.IsValid && !obj.IsDeleted)
                {
                    totalObjectCount++;
                    var layerIndex = obj.Attributes.LayerIndex;
                    
                    if (!objectsByLayer.ContainsKey(layerIndex))
                    {
                        objectsByLayer[layerIndex] = new List<RhinoObject>();
                    }
                    objectsByLayer[layerIndex].Add(obj);
                }
            }

            // Process layers - only non-deleted ones
            int activeLayerCount = 0;
            foreach (var layer in doc.Layers)
            {
                if (layer == null || layer.IsDeleted) continue;
                
                activeLayerCount++;
                var layerObjects = objectsByLayer.ContainsKey(layer.Index) 
                    ? objectsByLayer[layer.Index] 
                    : new List<RhinoObject>();

                var sampleObjects = new JArray();
                
                // Get sample objects - remove redundant layer info since it's already in layerData
                foreach (var obj in layerObjects.Take(SAMPLE_OBJECTS_PER_LAYER))
                {
                    try
                    {
                        var objData = new JObject
                        {
                            ["id"] = obj.Id.ToString(),
                            ["type"] = obj.Geometry?.GetType().Name ?? "Unknown",
                            ["name"] = obj.Attributes.Name ?? ""
                        };

                        // Add metadata only if available
                        var userText = obj.Attributes.GetUserStrings();
                        if (userText != null && userText.Count > 0)
                        {
                            var metadata = new JObject();
                            foreach (string key in userText.AllKeys)
                            {
                                metadata[key] = userText[key];
                            }
                            objData["metadata"] = metadata;
                        }

                        sampleObjects.Add(objData);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing object {obj.Id}: {ex.Message}");
                    }
                }

                var layerData = new JObject
                {
                    ["id"] = layer.Id.ToString(),
                    ["name"] = layer.Name,
                    ["full_path"] = layer.FullPath,
                    ["color"] = $"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}",
                    ["visible"] = layer.IsVisible,
                    ["locked"] = layer.IsLocked,
                    ["object_count"] = layerObjects.Count,
                    ["sample_objects"] = sampleObjects
                };

                layersData.Add(layerData);
            }

            var metaData = new JObject
            {
                ["name"] = doc.Name,
                ["date_created"] = doc.DateCreated,
                ["date_modified"] = doc.DateLastEdited,
                ["tolerance"] = doc.ModelAbsoluteTolerance,
                ["angle_tolerance"] = doc.ModelAngleToleranceDegrees,
                ["units"] = doc.ModelUnitSystem.ToString(),
                ["total_objects"] = totalObjectCount,
                ["total_layers"] = activeLayerCount
            };

            var result = new JObject
            {
                ["status"] = "success",
                ["document"] = metaData,
                ["layers"] = layersData,
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            Logger.Info($"Scene info collected for {layersData.Count} layers");
            return result;
        }
    }
}