using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("select_rhino_objects", "Select Rhino objects based on filters")]
    public class SelectRhinoObjects : ITool
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

                var filters = parameters["filters"] as JObject ?? new JObject();
                var filtersType = parameters["filters_type"]?.ToString() ?? "and";

                var allObjects = doc.Objects.ToList();
                var selectedObjects = new List<RhinoObject>();
                var selectedIds = new JArray();
                var unselectableObjects = new JArray();
                var filteredObjects = new List<RhinoObject>();

                // Clear current selection
                doc.Objects.UnselectAll();

                // First, find objects that match the filters
                if (filters.Count == 0)
                {
                    // No filters means all valid objects
                    filteredObjects.AddRange(allObjects.Where(obj => obj != null && obj.IsValid));
                }
                else
                {
                    // Apply filters based on filters_type
                    foreach (var obj in allObjects)
                    {
                        if (obj == null || !obj.IsValid) continue;

                        bool matches = (filtersType.ToLower() == "and") 
                            ? MatchesAllFilters(obj, filters, doc)
                            : MatchesAnyFilter(obj, filters, doc);

                        if (matches)
                        {
                            filteredObjects.Add(obj);
                        }
                    }
                }

                // Now attempt to select the filtered objects, checking selectability
                foreach (var obj in filteredObjects)
                {
                    if (IsObjectSelectable(obj, doc))
                    {
                        if (doc.Objects.Select(obj.Id))
                        {
                            selectedObjects.Add(obj);
                            selectedIds.Add(obj.Id.ToString());
                        }
                        else
                        {
                            // Selection failed for some other reason
                            unselectableObjects.Add(new JObject
                            {
                                ["object_id"] = obj.Id.ToString(),
                                ["name"] = obj.Attributes.Name ?? "",
                                ["reason"] = "Selection failed (unknown reason)"
                            });
                        }
                    }
                    else
                    {
                        // Object matches filter but isn't selectable
                        var reason = GetUnselectableReason(obj, doc);
                        unselectableObjects.Add(new JObject
                        {
                            ["object_id"] = obj.Id.ToString(),
                            ["name"] = obj.Attributes.Name ?? "",
                            ["reason"] = reason
                        });
                    }
                }

                // Update views to show selection
                doc.Views.Redraw();

                var result = new JObject
                {
                    ["status"] = "success",
                    ["total_filtered"] = filteredObjects.Count,
                    ["count"] = selectedObjects.Count,
                    ["selected_ids"] = selectedIds,
                };

                // Include information about unselectable objects if any
                if (unselectableObjects.Count > 0)
                {
                    result["unselectable_objects"] = unselectableObjects;
                    result["unselectable_count"] = unselectableObjects.Count;
                }

                return result;
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

        private bool IsObjectSelectable(RhinoObject rhinoObject, RhinoDoc doc)
        {
            // Check if object is locked
            if (rhinoObject.IsLocked)
                return false;

            // Check if object is hidden
            if (!rhinoObject.Visible)
                return false;

            // Check if object's layer is locked or hidden
            var layer = doc.Layers[rhinoObject.Attributes.LayerIndex];
            if (layer != null)
            {
                if (layer.IsLocked || !layer.IsVisible)
                    return false;
            }

            // Check if object has grips and if it's selectable with grips on
            if (rhinoObject.GripsOn)
            {
                // Objects with grips on are generally selectable, but we can add more checks if needed
                // The IsSelectableWithGripsOn method doesn't exist in the RhinoCommon API
                return true;
            }

            return true;
        }

        private string GetUnselectableReason(RhinoObject rhinoObject, RhinoDoc doc)
        {
            if (rhinoObject.IsLocked)
                return "Object is locked";

            if (!rhinoObject.Visible)
                return "Object is hidden";

            var layer = doc.Layers[rhinoObject.Attributes.LayerIndex];
            if (layer != null)
            {
                if (layer.IsLocked)
                    return "Object's layer is locked";
                if (!layer.IsVisible)
                    return "Object's layer is hidden";
            }

            if (rhinoObject.GripsOn)
                return "Object has grips on"; // Generally selectable, but noting grips state

            return "Unknown reason";
        }

        private bool MatchesAllFilters(RhinoObject rhinoObject, JObject filters, RhinoDoc doc)
        {
            foreach (var filter in filters.Properties())
            {
                if (!MatchesFilter(rhinoObject, filter.Name, filter.Value, doc))
                    return false;
            }
            return true;
        }

        private bool MatchesAnyFilter(RhinoObject rhinoObject, JObject filters, RhinoDoc doc)
        {
            foreach (var filter in filters.Properties())
            {
                if (MatchesFilter(rhinoObject, filter.Name, filter.Value, doc))
                    return true;
            }
            return false;
        }

        private bool MatchesFilter(RhinoObject rhinoObject, string filterName, JToken filterValue, RhinoDoc doc)
        {
            var filterValues = ParameterUtils.CastToStringList(filterValue);
            
            switch (filterName.ToLower())
            {
                case "name":
                    string objectName = rhinoObject.Attributes.Name ?? "";
                    return filterValues.Any(value => objectName.Equals(value, StringComparison.OrdinalIgnoreCase));

                case "color":
                    return MatchesColorFilter(rhinoObject, filterValue);

                case "layer":
                    string layerName = doc.Layers[rhinoObject.Attributes.LayerIndex].Name;
                    return filterValues.Any(value => layerName.Equals(value, StringComparison.OrdinalIgnoreCase));

                case "material":
                    return MatchesMaterialFilter(rhinoObject, filterValue);

                default:
                    // Handle custom attributes (user strings)
                    string attributeValue = rhinoObject.Attributes.GetUserString(filterName) ?? "";
                    return filterValues.Any(value => attributeValue.Equals(value, StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool MatchesColorFilter(RhinoObject rhinoObject, JToken colorFilter)
        {
            // Handle color as [R, G, B] array
            if (colorFilter is JArray colorArray && colorArray.Count >= 3)
            {
                var targetColor = ParameterUtils.GetColorFromToken(colorFilter);
                if (targetColor != null)
                {
                    var objectColor = rhinoObject.Attributes.ObjectColor;
                    return objectColor.R == targetColor[0] && 
                           objectColor.G == targetColor[1] && 
                           objectColor.B == targetColor[2];
                }
            }

            // Handle color as list of [R, G, B] arrays
            if (colorFilter is JArray filterArray)
            {
                foreach (var colorItem in filterArray)
                {
                    if (colorItem is JArray)
                    {
                        var targetColor = ParameterUtils.GetColorFromToken(colorItem);
                        if (targetColor != null)
                        {
                            var objectColor = rhinoObject.Attributes.ObjectColor;
                            if (objectColor.R == targetColor[0] && 
                                objectColor.G == targetColor[1] && 
                                objectColor.B == targetColor[2])
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool MatchesMaterialFilter(RhinoObject rhinoObject, JToken materialFilter)
        {
            // Get the object's material ID
            string objectMaterialId = null;
            if (rhinoObject.RenderMaterial != null)
            {
                objectMaterialId = rhinoObject.RenderMaterial.Id.ToString();
            }
            else
            {
                // Object uses layer default material - we'll use null to represent this
                objectMaterialId = null;
            }

            // Handle single material ID as string
            if (materialFilter.Type == JTokenType.String)
            {
                string targetMaterialId = materialFilter.ToString();
                
                // Handle special case for layer default material
                if (string.IsNullOrEmpty(targetMaterialId) || 
                    targetMaterialId.Equals("layer_default", StringComparison.OrdinalIgnoreCase) ||
                    targetMaterialId.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    return objectMaterialId == null;
                }
                
                return string.Equals(objectMaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase);
            }

            // Handle array of material IDs
            if (materialFilter is JArray materialArray)
            {
                foreach (var materialItem in materialArray)
                {
                    if (materialItem.Type == JTokenType.String)
                    {
                        string targetMaterialId = materialItem.ToString();
                        
                        // Handle special case for layer default material
                        if (string.IsNullOrEmpty(targetMaterialId) || 
                            targetMaterialId.Equals("layer_default", StringComparison.OrdinalIgnoreCase) ||
                            targetMaterialId.Equals("default", StringComparison.OrdinalIgnoreCase))
                        {
                            if (objectMaterialId == null)
                                return true;
                        }
                        else if (string.Equals(objectMaterialId, targetMaterialId, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}