using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Serializers;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("get_rhino_objects_info", "Get detailed information about objects in the active Rhino document, including metadata and filters", RequiresDocument = true)]
    public class GetRhinoObjectsInfo : ITool
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
                bool includeAttributes = parameters["include_attributes"]?.ToObject<bool>() ?? false;
                
                var objects = new JArray();
                int matchedCount = 0;

                foreach (var rhinoObject in doc.Objects)
                {
                    if (rhinoObject == null || !rhinoObject.IsValid) continue;

                    // Check if object matches filters
                    if (!MatchesFilters(rhinoObject, filters, doc)) continue;

                    var objectData = BuildObjectData(rhinoObject, includeAttributes, doc);
                    objects.Add(objectData);
                    matchedCount++;
                }

                return new JObject
                {
                    ["status"] = "success",
                    ["objects"] = objects,
                    ["count"] = matchedCount,
                    ["filters_applied"] = filters,
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting objects info: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error getting objects info: {ex.Message}"
                };
            }
        }

        private bool MatchesFilters(RhinoObject rhinoObject, JObject filters, RhinoDoc doc)
        {
            // Layer filter
            if (filters["layer"] != null)
            {
                string layerFilter = filters["layer"].ToString();
                var layer = doc.Layers[rhinoObject.Attributes.LayerIndex];
                string layerName = layer?.Name ?? "Default";
                
                if (!MatchesWildcard(layerName, layerFilter))
                    return false;
            }

            // Name filter
            if (filters["name"] != null)
            {
                string nameFilter = filters["name"].ToString();
                string objectName = rhinoObject.Attributes.Name ?? "";
                
                if (!MatchesWildcard(objectName, nameFilter))
                    return false;
            }

            // Object type filter
            if (filters["type"] != null)
            {
                string typeFilter = filters["type"].ToString();
                string objectType = rhinoObject.ObjectType.ToString();
                
                if (!MatchesWildcard(objectType, typeFilter))
                    return false;
            }

            // Description filter (from user text)
            if (filters["description"] != null)
            {
                string descriptionFilter = filters["description"].ToString();
                string description = rhinoObject.Attributes.GetUserString("description") ?? "";
                
                if (!MatchesWildcard(description, descriptionFilter))
                    return false;
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

        private JObject BuildObjectData(RhinoObject rhinoObject, bool includeAttributes, RhinoDoc doc)
        {
            // Use the standard serializer to get object info
            var objectData = Serializer.RhinoObject(rhinoObject);
            
            // Add description from user text if available
            string description = rhinoObject.Attributes.GetUserString("description");
            if (!string.IsNullOrEmpty(description))
            {
                objectData["description"] = description;
            }

            // Include all attributes if requested
            if (includeAttributes)
            {
                var attributes = Serializer.RhinoObjectAttributes(rhinoObject);
                if (attributes.Count > 0)
                {
                    objectData["attributes"] = attributes;
                }
            }

            return objectData;
        }
    }
}