using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Serializers;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("modify_rhino_objects", "Apply geometric transformations to objects in Rhino document", ModifiesDocument = true)]
    public class ModifyRhinoObjects : ITool
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

                // Check for "all" flag to modify all objects
                bool modifyAll = parameters["all"]?.Value<bool>() ?? false;
                
                // Get the modifications array
                JArray objectsToModify = null;
                
                if (parameters["objects"] is JArray objectsArray)
                {
                    objectsToModify = objectsArray;
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "No 'objects' array provided with modification parameters"
                    };
                }

                if (objectsToModify.Count == 0)
                {
                    return new JObject
                    {
                        ["error"] = "Objects array is empty"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Modify Rhino Objects");

                try
                {
                    var modifiedObjects = new JArray();
                    var results = new JObject();

                    // Handle "all" case with single modification set
                    if (modifyAll && objectsToModify.Count == 1)
                    {
                        var allObjects = doc.Objects.ToList();
                        var firstModification = objectsToModify[0] as JObject;
                        
                        if (firstModification != null)
                        {
                            // Apply the same modification to all objects
                            foreach (var obj in allObjects)
                            {
                                if (obj != null && obj.IsValid)
                                {
                                    try
                                    {
                                        // Create modification parameters with object ID
                                        var modParams = new JObject(firstModification)
                                        {
                                            ["id"] = obj.Id.ToString()
                                        };

                                        var result = ModifySingleObject(doc, modParams);
                                        modifiedObjects.Add(result);
                                        
                                        string objectKey = result["name"]?.ToString() ?? result["id"]?.ToString() ?? $"Object_{modifiedObjects.Count}";
                                        results[objectKey] = result;
                                    }
                                    catch (Exception ex)
                                    {
                                        var errorResult = new JObject
                                        {
                                            ["status"] = "error",
                                            ["error"] = ex.Message,
                                            ["id"] = obj.Id.ToString(),
                                            ["name"] = obj.Attributes.Name ?? "(unnamed)"
                                        };
                                        modifiedObjects.Add(errorResult);
                                        
                                        string objectKey = obj.Attributes.Name ?? obj.Id.ToString();
                                        results[objectKey] = errorResult;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Handle individual object modifications
                        foreach (var objectToken in objectsToModify)
                        {
                            var objectParams = objectToken as JObject;
                            if (objectParams == null || (!objectParams.ContainsKey("id") && !objectParams.ContainsKey("name"))) continue;

                            try
                            {
                                var result = ModifySingleObject(doc, objectParams);
                                modifiedObjects.Add(result);
                                
                                string objectKey = result["name"]?.ToString() ?? result["id"]?.ToString() ?? $"Object_{modifiedObjects.Count}";
                                results[objectKey] = result;
                            }
                            catch (Exception ex)
                            {
                                var errorResult = new JObject
                                {
                                    ["status"] = "error",
                                    ["error"] = ex.Message,
                                    ["id"] = objectParams["id"]?.ToString() ?? "unknown",
                                    ["name"] = objectParams["name"]?.ToString() ?? "unknown"
                                };
                                modifiedObjects.Add(errorResult);
                                
                                string objectKey = objectParams["name"]?.ToString() ?? objectParams["id"]?.ToString() ?? $"Error_{modifiedObjects.Count}";
                                results[objectKey] = errorResult;
                            }
                        }
                    }

                    // Update views
                    doc.Views.Redraw();

                    // End undo record
                    doc.EndUndoRecord(undoRecordSerialNumber);

                    return new JObject
                    {
                        ["status"] = "success",
                        ["objects_modified"] = modifiedObjects,
                        ["count"] = modifiedObjects.Count,
                        ["results"] = results
                    };
                }
                catch (Exception ex)
                {
                    // End undo record and undo changes
                    doc.EndUndoRecord(undoRecordSerialNumber);
                    doc.Undo();
                    throw;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error modifying objects: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error modifying objects: {ex.Message}"
                };
            }
        }

        private JObject ModifySingleObject(RhinoDoc doc, JObject objectParams)
        {
            // Find the object by ID or name
            RhinoObject rhinoObject = GetObjectByIdOrName(doc, objectParams);
            if (rhinoObject == null)
            {
                throw new InvalidOperationException("Object not found");
            }

            // Check if object is locked
            if (rhinoObject.IsLocked)
            {
                throw new InvalidOperationException($"Cannot modify locked object {rhinoObject.Id}");
            }

            // Store original info
            string objectName = rhinoObject.Attributes.Name ?? "(unnamed)";
            Guid objectId = rhinoObject.Id;
            var geometry = rhinoObject.Geometry;
            var modifications = new List<string>();

            // Build transformation matrix
            var xform = Transform.Identity;
            bool geometryModified = false;
            bool attributesModified = false;

            // Handle name change
            if (objectParams["new_name"] != null)
            {
                string newName = objectParams["new_name"].ToString();
                var attributes = rhinoObject.Attributes.Duplicate();
                attributes.Name = newName;
                doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                modifications.Add($"renamed to '{newName}'");
                attributesModified = true;
                objectName = newName; // Update for result
            }

            // Handle color change
            if (objectParams["new_color"] != null)
            {
                var color = ParameterUtils.GetColorFromToken(objectParams["new_color"]);
                if (color != null)
                {
                    var attributes = rhinoObject.Attributes.Duplicate();
                    attributes.ObjectColor = Color.FromArgb(color[0], color[1], color[2]);
                    attributes.ColorSource = ObjectColorSource.ColorFromObject;
                    doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                    modifications.Add($"color changed to RGB({color[0]}, {color[1]}, {color[2]})");
                    attributesModified = true;
                }
            }

            // Apply translation
            if (objectParams["translation"] != null)
            {
                var translation = ApplyTranslation(objectParams);
                xform = xform * translation;
                geometryModified = true;
                modifications.Add("translation applied");
            }

            // Apply scale
            if (objectParams["scale"] != null)
            {
                var scale = ApplyScale(objectParams, geometry);
                xform = xform * scale;
                geometryModified = true;
                modifications.Add("scaling applied");
            }

            // Apply rotation
            if (objectParams["rotation"] != null)
            {
                var rotation = ApplyRotation(objectParams, geometry);
                xform = xform * rotation;
                geometryModified = true;
                modifications.Add("rotation applied");
            }

            // Apply geometric transformations
            if (geometryModified)
            {
                Guid transformedId = doc.Objects.Transform(objectId, xform, true);
                if (transformedId == Guid.Empty)
                {
                    throw new InvalidOperationException("Failed to apply geometric transformation");
                }
            }

            // Get updated object for result
            var updatedObject = doc.Objects.Find(objectId);
            var result = Serializer.RhinoObject(updatedObject ?? rhinoObject);
            result["status"] = "success";
            result["modifications"] = new JArray(modifications);
            result["geometry_modified"] = geometryModified;
            result["attributes_modified"] = attributesModified;
            
            return result;
        }

        private RhinoObject GetObjectByIdOrName(RhinoDoc doc, JObject parameters)
        {
            // Try by ID first
            if (parameters["id"] != null)
            {
                string idStr = parameters["id"].ToString();
                if (Guid.TryParse(idStr, out Guid objectId))
                {
                    return doc.Objects.Find(objectId);
                }
            }

            // Try by name
            if (parameters["name"] != null)
            {
                string name = parameters["name"].ToString();
                return doc.Objects.FirstOrDefault(obj => 
                    obj != null && 
                    obj.IsValid && 
                    string.Equals(obj.Attributes.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private Transform ApplyTranslation(JObject parameters)
        {
            return ParameterUtils.GetTranslationTransform(parameters, "translation");
        }

        private Transform ApplyScale(JObject parameters, GeometryBase geometry)
        {
            // Get bounding box center as scale anchor
            var bbox = geometry.GetBoundingBox(true);
            var anchor = bbox.IsValid ? bbox.Center : Point3d.Origin;
            
            return ParameterUtils.GetScaleTransform(parameters, anchor, "scale");
        }

        private Transform ApplyRotation(JObject parameters, GeometryBase geometry)
        {
            // Get bounding box center as rotation center
            var bbox = geometry.GetBoundingBox(true);
            var center = bbox.IsValid ? bbox.Center : Point3d.Origin;
            
            return ParameterUtils.GetRotationTransform(parameters, center, "rotation");
        }
    }
}
