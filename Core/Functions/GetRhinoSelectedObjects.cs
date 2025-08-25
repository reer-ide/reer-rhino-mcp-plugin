using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Core.Common;

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

                // Get parameters for lights and grips
                bool includeLights = ParameterUtils.GetBoolValue(parameters, "include_lights", false);
                bool includeGrips = ParameterUtils.GetBoolValue(parameters, "include_grips", false);
                
                // Use GetObject approach to handle both full objects and subobjects
                var selectedObjectsDict = new Dictionary<Guid, JObject>();
                var selectedObjects = new JArray();
                int totalSelectionCount = 0;
                
                Logger.Debug("Checking sub-objects selection...");
                
                // Create GetObject for interactive selection
                using (var go = new GetObject())
                {
                    go.SubObjectSelect = true; // Enable subobject selection
                    go.DeselectAllBeforePostSelect = false;
                    go.EnablePreSelect(true, true);
                    go.EnablePostSelect(false);
                    
                    // Set geometry filter to exclude/include specific types
                    var geometryFilter = Rhino.DocObjects.ObjectType.AnyObject;
                    if (!includeLights)
                    {
                        geometryFilter &= ~Rhino.DocObjects.ObjectType.Light;
                    }
                    go.GeometryFilter = geometryFilter;
                    
                    // Get multiple objects/subobjects
                    var result = go.GetMultiple(1, 0); // min=1, max=0 means at least 1 object required
                    
                    if (result == Rhino.Input.GetResult.Object)
                    {
                        totalSelectionCount = go.ObjectCount; // Total count includes all selections (objects + subobjects)
                        Logger.Info($"Total selected items (including subobjects): {totalSelectionCount}");
                        for (int i = 0; i < totalSelectionCount; i++)
                        {
                            var objRef = go.Object(i);
                            var obj = objRef.Object();

                            if (obj == null || !obj.IsValid) continue;

                            // Filter based on grips setting
                            if (!includeGrips && obj.GripsOn)
                            {
                                continue; // Skip objects with grips on if not including grips
                            }

                            // Filter based on lights setting (double check since GeometryFilter might not catch all)
                            if (!includeLights && obj.ObjectType == Rhino.DocObjects.ObjectType.Light)
                            {
                                continue; // Skip light objects if not including lights
                            }

                            var componentIndex = objRef.GeometryComponentIndex;
                            var objId = obj.Id;

                            // Check if this is a subobject selection
                            if (componentIndex.ComponentIndexType != ComponentIndexType.InvalidType &&
                                componentIndex.ComponentIndexType != ComponentIndexType.BrepLoop)
                            {
                                // This is a subobject selection
                                if (selectedObjectsDict.ContainsKey(objId))
                                {
                                    // Add to existing subobject list
                                    var subobjects = selectedObjectsDict[objId]["subobjects"] as JArray;
                                    subobjects?.Add(new JObject
                                    {
                                        ["index"] = componentIndex.Index,
                                        ["type"] = componentIndex.ComponentIndexType.ToString()
                                    });
                                }
                                else
                                {
                                    // Create new entry for subobject selection
                                    var objData = BuildObjectData(obj, doc);
                                    objData["selection_type"] = "subobject";
                                    objData["subobjects"] = new JArray
                                    {
                                        new JObject
                                        {
                                            ["index"] = componentIndex.Index,
                                            ["type"] = componentIndex.ComponentIndexType.ToString()
                                        }
                                    };
                                    selectedObjectsDict[objId] = objData;
                                }
                            }
                            else
                            {
                                // This is a full object selection
                                if (!selectedObjectsDict.ContainsKey(objId))
                                {
                                    var objData = BuildObjectData(obj, doc);
                                    objData["selection_type"] = "full";
                                    selectedObjectsDict[objId] = objData;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Handle case where user cancelled or no objects selected
                        Logger.Info("No objects selected or operation cancelled.");
                        return new JObject
                        {
                            ["error"] = "No objects selected"
                        };
                    }
                }
                
                // Convert dictionary to array for response
                foreach (var objData in selectedObjectsDict.Values)
                {
                    selectedObjects.Add(objData);
                }

                return new JObject
                {
                    ["status"] = "success",
                    ["selected_count"] = totalSelectionCount, // Total items selected (including subobjects)
                    ["unique_objects_count"] = selectedObjectsDict.Count, // Number of unique parent objects
                    ["selected_objects"] = selectedObjects,
                    ["include_lights"] = includeLights,
                    ["include_grips"] = includeGrips
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting selected objects: {ex.Message}");
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
                ["type"] = GetObjectTypeName(rhinoObject),
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

        private string GetObjectTypeName(RhinoObject rhinoObject)
        {
            // Use the actual object type rather than geometry type
            switch (rhinoObject.ObjectType)
            {
                case ObjectType.Point:
                    return "Point";
                case ObjectType.PointSet:
                    return "PointCloud";
                case ObjectType.Curve:
                    return "Curve";
                case ObjectType.Surface:
                    return "Surface";
                case ObjectType.Brep:
                    return "Brep";
                case ObjectType.Mesh:
                    return "Mesh";
                case ObjectType.Light:
                    return "Light";
                case ObjectType.Annotation:
                    return "Annotation";
                case ObjectType.InstanceDefinition:
                    return "Block";
                case ObjectType.InstanceReference:
                    return "BlockInstance";
                case ObjectType.TextDot:
                    return "TextDot";
                case ObjectType.Grip:
                    return "Grip";
                case ObjectType.Detail:
                    return "Detail";
                case ObjectType.Hatch:
                    return "Hatch";
                case ObjectType.MorphControl:
                    return "MorphControl";
                case ObjectType.BrepLoop:
                    return "BrepLoop";
                case ObjectType.PolysrfFilter:
                    return "Polysurface";
                case ObjectType.EdgeFilter:
                    return "Edge";
                case ObjectType.PolyedgeFilter:
                    return "Polyedge";
                case ObjectType.MeshVertex:
                    return "MeshVertex";
                case ObjectType.MeshEdge:
                    return "MeshEdge";
                case ObjectType.MeshFace:
                    return "MeshFace";
                case ObjectType.Cage:
                    return "Cage";
                case ObjectType.Phantom:
                    return "Phantom";
                case ObjectType.ClipPlane:
                    return "ClippingPlane";
                case ObjectType.Extrusion:
                    return "Extrusion";
                default:
                    // Fallback to geometry type if object type is not recognized
                    return rhinoObject.Geometry?.GetType().Name ?? "Unknown";
            }
        }
    }
}