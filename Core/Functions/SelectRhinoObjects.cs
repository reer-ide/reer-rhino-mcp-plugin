using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("select_rhino_objects", "Select Rhino objects based on various criteria")]
    public class SelectRhinoObjects
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

                var selectionCriteria = parameters["selection_criteria"] as JObject;
                if (selectionCriteria == null)
                {
                    return new JObject
                    {
                        ["error"] = "No selection criteria provided"
                    };
                }

                var selectedObjects = new List<RhinoObject>();
                var selectedIds = new JArray();
                var selectedMetadata = new JArray();

                // Clear current selection
                doc.Objects.UnselectAll();

                foreach (var rhinoObject in doc.Objects)
                {
                    if (rhinoObject == null || !rhinoObject.IsValid) continue;

                    if (MatchesSelectionCriteria(rhinoObject, selectionCriteria, doc))
                    {
                        selectedObjects.Add(rhinoObject);
                        selectedIds.Add(rhinoObject.Id.ToString());

                        // Get object metadata
                        var objectData = BuildSelectedObjectData(rhinoObject, doc);
                        selectedMetadata.Add(objectData);

                        // Select the object in Rhino
                        rhinoObject.Select(true);
                    }
                }

                // Update views to show selection
                doc.Views.Redraw();

                return new JObject
                {
                    ["status"] = "success",
                    ["selected_count"] = selectedObjects.Count,
                    ["selected_ids"] = selectedIds,
                    ["selected_objects"] = selectedMetadata,
                    ["criteria"] = selectionCriteria
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error selecting objects: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error selecting objects: {ex.Message}"
                };
            }
        }

        private bool MatchesSelectionCriteria(RhinoObject rhinoObject, JObject criteria, RhinoDoc doc)
        {
            var userStrings = rhinoObject.Attributes.GetUserStrings();

            // Name criteria
            if (criteria["name"] != null)
            {
                string namePattern = criteria["name"].ToString();
                string objectName = rhinoObject.Attributes.Name ?? "";
                if (!MatchesWildcard(objectName, namePattern))
                    return false;
            }

            // Layer criteria
            if (criteria["layer"] != null)
            {
                string layerPattern = criteria["layer"].ToString();
                var layer = doc.Layers[rhinoObject.Attributes.LayerIndex];
                string layerPath = layer?.FullPath ?? "Default";
                if (!MatchesWildcard(layerPath, layerPattern))
                    return false;
            }

            // Type criteria
            if (criteria["type"] != null)
            {
                string typePattern = criteria["type"].ToString();
                string objectType = rhinoObject.Geometry?.GetType().Name ?? "Unknown";
                if (!MatchesWildcard(objectType, typePattern))
                    return false;
            }

            // Short ID criteria (exact match)
            if (criteria["short_id"] != null)
            {
                string shortIdFilter = criteria["short_id"].ToString();
                string objectShortId = userStrings.Get("short_id") ?? "";
                if (objectShortId != shortIdFilter)
                    return false;
            }

            // Custom metadata criteria
            if (criteria["metadata"] is JObject metadataCriteria)
            {
                foreach (var criterion in metadataCriteria.Properties())
                {
                    string metadataValue = userStrings.Get(criterion.Name) ?? "";
                    string criterionValue = criterion.Value.ToString();
                    
                    if (!MatchesWildcard(metadataValue, criterionValue))
                        return false;
                }
            }

            // Bounding box criteria
            if (criteria["bbox"] is JObject bboxCriteria)
            {
                var bbox = rhinoObject.Geometry?.GetBoundingBox(true);
                if (!bbox.HasValue || !bbox.Value.IsValid)
                    return false;

                var box = bbox.Value;

                // Check if object's bounding box intersects with the criteria box
                if (bboxCriteria["min"] is JArray minArray && bboxCriteria["max"] is JArray maxArray)
                {
                    if (minArray.Count >= 3 && maxArray.Count >= 3)
                    {
                        double minX = GetDoubleFromToken(minArray[0]);
                        double minY = GetDoubleFromToken(minArray[1]);
                        double minZ = GetDoubleFromToken(minArray[2]);
                        double maxX = GetDoubleFromToken(maxArray[0]);
                        double maxY = GetDoubleFromToken(maxArray[1]);
                        double maxZ = GetDoubleFromToken(maxArray[2]);

                        // Check if bounding boxes intersect
                        if (box.Max.X < minX || box.Min.X > maxX ||
                            box.Max.Y < minY || box.Min.Y > maxY ||
                            box.Max.Z < minZ || box.Min.Z > maxZ)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private bool MatchesWildcard(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (pattern == "*") return true;
            
            // Simple wildcard matching
            if (pattern.Contains("*"))
            {
                var regex = new System.Text.RegularExpressions.Regex(
                    "^" + pattern.Replace("*", ".*") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                return regex.IsMatch(text);
            }
            
            return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private JObject BuildSelectedObjectData(RhinoObject rhinoObject, RhinoDoc doc)
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

            // Include all metadata
            var metadata = new JObject();
            foreach (string key in userStrings.AllKeys)
            {
                metadata[key] = userStrings[key];
            }

            if (metadata.Count > 0)
            {
                objectData["metadata"] = metadata;
            }

            return objectData;
        }

        private double GetDoubleFromToken(JToken token)
        {
            if (token != null && double.TryParse(token.ToString(), out double result))
                return result;
            return 0;
        }
    }
}