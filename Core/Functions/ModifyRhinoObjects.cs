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
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("modify_rhino_objects", "Apply geometric transformations and attribute changes to Rhino objects with support for chained operations", ModifiesDocument = true)]
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

                // Parse targets
                var targets = ParseTargets(doc, parameters);
                if (!targets.Any())
                {
                    return new JObject
                    {
                        ["error"] = "No valid targets found"
                    };
                }

                // Parse operations
                var operations = ParseOperations(parameters);
                if (!operations.Any())
                {
                    return new JObject
                    {
                        ["error"] = "No operations specified"
                    };
                }

                // Get execution mode
                string executionMode = parameters["execution"]?.ToString() ?? "combined";

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Modify Rhino Objects");

                try
                {
                    var modifiedObjects = new JArray();
                    var results = new JObject();

                    // Process each target object
                    foreach (var targetObj in targets)
                    {
                        try
                        {
                            var result = executionMode == "sequential" 
                                ? ApplyOperationsSequentially(doc, targetObj, operations)
                                : ApplyOperationsCombined(doc, targetObj, operations);
                            
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
                                ["id"] = targetObj.Id.ToString(),
                                ["name"] = targetObj.Attributes.Name ?? "(unnamed)"
                            };
                            modifiedObjects.Add(errorResult);
                            
                            string objectKey = targetObj.Attributes.Name ?? targetObj.Id.ToString();
                            results[objectKey] = errorResult;
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
                        ["execution_mode"] = executionMode,
                        ["operations_count"] = operations.Count,
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
                Logger.Error($"Error modifying objects: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error modifying objects: {ex.Message}"
                };
            }
        }

        private List<RhinoObject> ParseTargets(RhinoDoc doc, JObject parameters)
        {
            var targets = new List<RhinoObject>();
            
            if (parameters["targets"] is JArray targetsArray)
            {
                foreach (var targetToken in targetsArray)
                {
                    if (targetToken is JObject targetObj)
                    {
                        if (targetObj["all"]?.Value<bool>() == true)
                        {
                            targets.AddRange(doc.Objects.Where(obj => obj != null && obj.IsValid && !obj.IsLocked));
                            break;
                        }
                        
                        var rhinoObj = GetObjectByIdOrName(doc, targetObj);
                        if (rhinoObj != null)
                        {
                            targets.Add(rhinoObj);
                        }
                    }
                }
            }
            
            return targets.Distinct().ToList();
        }

        private List<JObject> ParseOperations(JObject parameters)
        {
            var operations = new List<JObject>();
            
            if (parameters["operations"] is JArray operationsArray)
            {
                foreach (var opToken in operationsArray)
                {
                    if (opToken is JObject opObj)
                    {
                        operations.Add(opObj);
                    }
                }
            }
            
            return operations;
        }

        private JObject ApplyOperationsSequentially(RhinoDoc doc, RhinoObject rhinoObject, List<JObject> operations)
        {
            var modifications = new List<string>();
            bool geometryModified = false;
            bool attributesModified = false;
            string objectName = rhinoObject.Attributes.Name ?? "(unnamed)";
            Guid objectId = rhinoObject.Id;

            foreach (var operation in operations)
            {
                var result = ApplySingleOperation(doc, rhinoObject, operation);
                modifications.AddRange(result.modifications);
                geometryModified = geometryModified || result.geometryModified;
                attributesModified = attributesModified || result.attributesModified;
                
                // Update reference to object after each operation
                rhinoObject = doc.Objects.Find(objectId) ?? rhinoObject;
                if (!string.IsNullOrEmpty(result.newName))
                {
                    objectName = result.newName;
                }
            }

            var updatedObject = doc.Objects.Find(objectId);
            var serializedResult = Serializer.RhinoObject(updatedObject ?? rhinoObject);
            serializedResult["status"] = "success";
            serializedResult["modifications"] = new JArray(modifications);
            serializedResult["geometry_modified"] = geometryModified;
            serializedResult["attributes_modified"] = attributesModified;
            serializedResult["execution_mode"] = "sequential";
            
            return serializedResult;
        }

        private JObject ApplyOperationsCombined(RhinoDoc doc, RhinoObject rhinoObject, List<JObject> operations)
        {
            var modifications = new List<string>();
            var xform = Transform.Identity;
            bool geometryModified = false;
            bool attributesModified = false;
            string objectName = rhinoObject.Attributes.Name ?? "(unnamed)";
            Guid objectId = rhinoObject.Id;

            foreach (var operation in operations)
            {
                var result = ProcessOperationForCombined(doc, rhinoObject, operation, ref xform);
                modifications.AddRange(result.modifications);
                geometryModified = geometryModified || result.geometryModified;
                attributesModified = attributesModified || result.attributesModified;
                if (!string.IsNullOrEmpty(result.newName))
                {
                    objectName = result.newName;
                }
            }

            // Apply combined transformation if geometry was modified
            if (geometryModified && !xform.IsIdentity)
            {
                Guid transformedId = doc.Objects.Transform(objectId, xform, true);
                if (transformedId == Guid.Empty)
                {
                    throw new InvalidOperationException("Failed to apply combined geometric transformation");
                }
            }

            var updatedObject = doc.Objects.Find(objectId);
            var serializedResult = Serializer.RhinoObject(updatedObject ?? rhinoObject);
            serializedResult["status"] = "success";
            serializedResult["modifications"] = new JArray(modifications);
            serializedResult["geometry_modified"] = geometryModified;
            serializedResult["attributes_modified"] = attributesModified;
            serializedResult["execution_mode"] = "combined";
            
            return serializedResult;
        }

        private (List<string> modifications, bool geometryModified, bool attributesModified, string newName) ApplySingleOperation(RhinoDoc doc, RhinoObject rhinoObject, JObject operation)
        {
            if (rhinoObject.IsLocked)
            {
                throw new InvalidOperationException($"Cannot modify locked object {rhinoObject.Id}");
            }

            string operationType = operation["type"]?.ToString();
            if (string.IsNullOrEmpty(operationType))
            {
                throw new InvalidOperationException("Operation type not specified");
            }

            var modifications = new List<string>();
            bool geometryModified = false;
            bool attributesModified = false;
            string newName = null;
            var geometry = rhinoObject.Geometry;
            Guid objectId = rhinoObject.Id;

            switch (operationType.ToLower())
            {
                case "translate":
                    var translateTransform = GetTranslationTransform(operation);
                    doc.Objects.Transform(objectId, translateTransform, true);
                    modifications.Add($"translated by {operation["vector"]}");
                    geometryModified = true;
                    break;

                case "rotate":
                    var rotateTransform = GetRotationTransform(operation, geometry);
                    doc.Objects.Transform(objectId, rotateTransform, true);
                    modifications.Add($"rotated by {operation["angle"]}° around {operation["axis"]}");
                    geometryModified = true;
                    break;

                case "scale":
                    var scaleTransform = GetScaleTransform(operation, geometry);
                    doc.Objects.Transform(objectId, scaleTransform, true);
                    modifications.Add($"scaled by {operation["factor"]}");
                    geometryModified = true;
                    break;

                case "rename":
                    newName = operation["name"]?.ToString();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        var attributes = rhinoObject.Attributes.Duplicate();
                        attributes.Name = newName;
                        doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                        modifications.Add($"renamed to '{newName}'");
                        attributesModified = true;
                    }
                    break;

                case "recolor":
                    var color = GetColorFromOperation(operation);
                    if (color != null)
                    {
                        var attributes = rhinoObject.Attributes.Duplicate();
                        attributes.ObjectColor = Color.FromArgb(color[0], color[1], color[2]);
                        attributes.ColorSource = ObjectColorSource.ColorFromObject;
                        doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                        modifications.Add($"color changed to RGB({color[0]}, {color[1]}, {color[2]})");
                        attributesModified = true;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown operation type: {operationType}");
            }

            return (modifications, geometryModified, attributesModified, newName);
        }

        private (List<string> modifications, bool geometryModified, bool attributesModified, string newName) ProcessOperationForCombined(RhinoDoc doc, RhinoObject rhinoObject, JObject operation, ref Transform combinedTransform)
        {
            string operationType = operation["type"]?.ToString();
            if (string.IsNullOrEmpty(operationType))
            {
                throw new InvalidOperationException("Operation type not specified");
            }

            var modifications = new List<string>();
            bool geometryModified = false;
            bool attributesModified = false;
            string newName = null;
            var geometry = rhinoObject.Geometry;

            switch (operationType.ToLower())
            {
                case "translate":
                    var translateTransform = GetTranslationTransform(operation);
                    combinedTransform = combinedTransform * translateTransform;
                    modifications.Add($"translate by {operation["vector"]}");
                    geometryModified = true;
                    break;

                case "rotate":
                    var rotateTransform = GetRotationTransform(operation, geometry);
                    combinedTransform = combinedTransform * rotateTransform;
                    modifications.Add($"rotate by {operation["angle"]}° around {operation["axis"]}");
                    geometryModified = true;
                    break;

                case "scale":
                    var scaleTransform = GetScaleTransform(operation, geometry);
                    combinedTransform = combinedTransform * scaleTransform;
                    modifications.Add($"scale by {operation["factor"]}");
                    geometryModified = true;
                    break;

                case "rename":
                    newName = operation["name"]?.ToString();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        var attributes = rhinoObject.Attributes.Duplicate();
                        attributes.Name = newName;
                        doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                        modifications.Add($"rename to '{newName}'");
                        attributesModified = true;
                    }
                    break;

                case "recolor":
                    var color = GetColorFromOperation(operation);
                    if (color != null)
                    {
                        var attributes = rhinoObject.Attributes.Duplicate();
                        attributes.ObjectColor = Color.FromArgb(color[0], color[1], color[2]);
                        attributes.ColorSource = ObjectColorSource.ColorFromObject;
                        doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                        modifications.Add($"recolor to RGB({color[0]}, {color[1]}, {color[2]})");
                        attributesModified = true;
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown operation type: {operationType}");
            }

            return (modifications, geometryModified, attributesModified, newName);
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

        private Transform GetTranslationTransform(JObject operation)
        {
            if (operation["vector"] is JArray vectorArray && vectorArray.Count >= 3)
            {
                double x = vectorArray[0].Value<double>();
                double y = vectorArray[1].Value<double>();
                double z = vectorArray[2].Value<double>();
                return Transform.Translation(new Vector3d(x, y, z));
            }
            throw new InvalidOperationException("Translation operation requires a 'vector' parameter with [x, y, z] values");
        }

        private Transform GetRotationTransform(JObject operation, GeometryBase geometry)
        {
            double angle = operation["angle"]?.Value<double>() ?? 0;
            var axis = Vector3d.ZAxis; // Default axis
            
            if (operation["axis"] is JArray axisArray && axisArray.Count >= 3)
            {
                axis = new Vector3d(
                    axisArray[0].Value<double>(),
                    axisArray[1].Value<double>(),
                    axisArray[2].Value<double>()
                );
            }
            
            Point3d center = Point3d.Origin;
            string centerType = operation["center"]?.ToString() ?? "auto";
            
            if (centerType == "auto")
            {
                var bbox = geometry.GetBoundingBox(true);
                center = bbox.IsValid ? bbox.Center : Point3d.Origin;
            }
            else if (centerType == "origin")
            {
                center = Point3d.Origin;
            }
            else if (operation["center"] is JArray centerArray && centerArray.Count >= 3)
            {
                center = new Point3d(
                    centerArray[0].Value<double>(),
                    centerArray[1].Value<double>(),
                    centerArray[2].Value<double>()
                );
            }
            
            return Transform.Rotation(RhinoMath.ToRadians(angle), axis, center);
        }

        private Transform GetScaleTransform(JObject operation, GeometryBase geometry)
        {
            double factor = operation["factor"]?.Value<double>() ?? 1.0;
            
            Point3d center = Point3d.Origin;
            string centerType = operation["center"]?.ToString() ?? "auto";
            
            if (centerType == "auto")
            {
                var bbox = geometry.GetBoundingBox(true);
                center = bbox.IsValid ? bbox.Center : Point3d.Origin;
            }
            else if (centerType == "origin")
            {
                center = Point3d.Origin;
            }
            else if (operation["center"] is JArray centerArray && centerArray.Count >= 3)
            {
                center = new Point3d(
                    centerArray[0].Value<double>(),
                    centerArray[1].Value<double>(),
                    centerArray[2].Value<double>()
                );
            }
            
            return Transform.Scale(center, factor);
        }

        private int[] GetColorFromOperation(JObject operation)
        {
            if (operation["color"] is JArray colorArray && colorArray.Count >= 3)
            {
                return new int[]
                {
                    colorArray[0].Value<int>(),
                    colorArray[1].Value<int>(),
                    colorArray[2].Value<int>()
                };
            }
            return null;
        }
    }
}
