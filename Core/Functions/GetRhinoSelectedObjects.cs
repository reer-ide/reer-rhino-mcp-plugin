using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("get_rhino_selected_objects", "Get information about currently selected objects in Rhino")]
    public class GetRhinoSelectedObjects : ITool
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                {
                    return new JObject
                    {
                        ["error"] = "No active Rhino document"
                    };
                }

                bool includeLights = ParameterUtils.GetBoolValue(parameters, "include_lights", false);
                bool includeGrips = ParameterUtils.GetBoolValue(parameters, "include_grips", false);

                var selectedObjects = new JArray();
                var selectedIds = new JArray();
                int count = 0;

                // Get selected objects
                var selectedRhinoObjects = doc.Objects.GetSelectedObjects(includeLights, includeGrips);

                foreach (var rhinoObject in selectedRhinoObjects)
                {
                    if (rhinoObject == null || !rhinoObject.IsValid) continue;

                    var objectData = BuildObjectData(rhinoObject, doc);
                    selectedObjects.Add(objectData);
                    selectedIds.Add(rhinoObject.Id.ToString());
                    count++;
                }

                return new JObject
                {
                    ["status"] = "success",
                    ["selected_count"] = count,
                    ["selected_ids"] = selectedIds,
                    ["selected_objects"] = selectedObjects,
                    ["include_lights"] = includeLights,
                    ["include_grips"] = includeGrips,
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting selected objects: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error getting selected objects: {ex.Message}"
                };
            }
        }

        private JObject BuildObjectData(RhinoObject rhinoObject, RhinoDoc doc)
        {
            var userStrings = rhinoObject.Attributes.GetUserStrings();
            
            var objectData = new JObject
            {
                ["id"] = rhinoObject.Id.ToString(),
                ["type"] = rhinoObject.Geometry?.GetType().Name ?? "Unknown",
                ["name"] = rhinoObject.Attributes.Name ?? ""
            };

            // Layer information
            var layer = doc.Layers[rhinoObject.Attributes.LayerIndex];
            objectData["layer"] = layer?.FullPath ?? "Default";

            // Bounding box
            var bbox = rhinoObject.Geometry?.GetBoundingBox(true);
            if (bbox.HasValue && bbox.Value.IsValid)
            {
                var box = bbox.Value;
                objectData["bbox"] = new JArray
                {
                    new JArray { box.Min.X, box.Min.Y, box.Min.Z },
                    new JArray { box.Max.X, box.Max.Y, box.Max.Z }
                };
            }

            // Object color
            if (rhinoObject.Attributes.ColorSource == ObjectColorSource.ColorFromObject)
            {
                var color = rhinoObject.Attributes.ObjectColor;
                objectData["color"] = new JArray { color.R, color.G, color.B };
            }

            // Include all metadata from user strings
            var metadata = new JObject();
            foreach (string key in userStrings.AllKeys)
            {
                metadata[key] = ParseMetadataValue(userStrings[key]);
            }

            if (metadata.Count > 0)
            {
                objectData["metadata"] = metadata;
            }

            // Selection status
            objectData["selected"] = rhinoObject.IsSelected(false) > 0;

            return objectData;
        }

        private JToken ParseMetadataValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            // Try to parse as JSON first (for arrays and objects)
            if ((value.StartsWith("[") && value.EndsWith("]")) || 
                (value.StartsWith("{") && value.EndsWith("}")))
            {
                try
                {
                    return JToken.Parse(value);
                }
                catch
                {
                    // If JSON parsing fails, return as string
                    return value;
                }
            }

            // Try to parse as number
            if (double.TryParse(value, out double numValue))
            {
                return numValue;
            }

            // Try to parse as boolean
            if (bool.TryParse(value, out bool boolValue))
            {
                return boolValue;
            }

            // Return as string
            return value;
        }
    }
}