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
    [MCPTool("delete_rhino_objects", "Delete objects from Rhino document", ModifiesDocument = true)]
    public class DeleteRhinoObjects : ITool
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

                // Check for "all" flag to delete all objects
                bool deleteAll = parameters["all"]?.Value<bool>() ?? false;
                
                if (deleteAll)
                {
                    return DeleteAllObjects(doc);
                }

                // Handle both single object and multiple objects input
                JArray objectsToDelete = null;
                
                // Check if we have an "objects" array
                if (parameters["objects"] is JArray objectsArray)
                {
                    objectsToDelete = objectsArray;
                }
                // Fall back to single object format for backward compatibility
                else if (parameters["id"] != null || parameters["name"] != null)
                {
                    objectsToDelete = new JArray { parameters };
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "No object identifiers provided (id, name, or 'all' flag required)"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Delete Rhino Objects");

                try
                {
                    var deletedObjects = new JArray();
                    var results = new JObject();

                    foreach (var objectToken in objectsToDelete)
                    {
                        var objectParams = objectToken as JObject;
                        if (objectParams == null) continue;

                        try
                        {
                            var result = DeleteSingleObject(doc, objectParams);
                            deletedObjects.Add(result);
                            
                            // Add to results with object identifier as key
                            string objectKey = result["name"]?.ToString() ?? result["id"]?.ToString() ?? $"Object_{deletedObjects.Count}";
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
                            deletedObjects.Add(errorResult);
                            
                            string objectKey = objectParams["name"]?.ToString() ?? objectParams["id"]?.ToString() ?? $"Error_{deletedObjects.Count}";
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
                        ["objects_deleted"] = deletedObjects,
                        ["count"] = deletedObjects.Count,
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
                Logger.Error($"Error deleting objects: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error deleting objects: {ex.Message}"
                };
            }
        }

        private JObject DeleteAllObjects(RhinoDoc doc)
        {
            var undoRecordSerialNumber = doc.BeginUndoRecord("Delete All Objects");
            
            try
            {
                var allObjects = doc.Objects.ToList();
                int objectCount = allObjects.Count;

                // Clear all objects
                doc.Objects.Clear();
                doc.Views.Redraw();

                doc.EndUndoRecord(undoRecordSerialNumber);

                return new JObject
                {
                    ["status"] = "success",
                    ["deleted"] = true,
                    ["count"] = objectCount,
                    ["message"] = $"Deleted all {objectCount} objects"
                };
            }
            catch (Exception)
            {
                doc.EndUndoRecord(undoRecordSerialNumber);
                doc.Undo();
                throw;
            }
        }

        private JObject DeleteSingleObject(RhinoDoc doc, JObject objectParams)
        {
            bool hasId = objectParams.ContainsKey("id");
            bool hasName = objectParams.ContainsKey("name");
            bool quietDelete = objectParams["quiet"]?.Value<bool>() ?? true; // Default to quiet delete

            if (!hasId && !hasName)
            {
                throw new InvalidOperationException("Either 'id' or 'name' must be provided");
            }

            // Find the object
            RhinoObject rhinoObject = null;
            
            if (hasId)
            {
                string idStr = objectParams["id"]?.ToString();
                if (Guid.TryParse(idStr, out Guid objectId))
                {
                    rhinoObject = doc.Objects.Find(objectId);
                }
                else
                {
                    throw new ArgumentException($"Invalid GUID format: {idStr}");
                }
            }
            else if (hasName)
            {
                string name = objectParams["name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    // Find object by name
                    rhinoObject = doc.Objects.FirstOrDefault(obj => 
                        obj != null && 
                        obj.IsValid && 
                        string.Equals(obj.Attributes.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (rhinoObject == null)
            {
                throw new InvalidOperationException("Object not found");
            }

            // Store object info before deletion
            Guid objId = rhinoObject.Id;
            string objectName = rhinoObject.Attributes.Name ?? "(unnamed)";
            string objectType = rhinoObject.ObjectType.ToString();

            // Check if object can be deleted (not locked, etc.)
            if (rhinoObject.IsLocked)
            {
                throw new InvalidOperationException($"Cannot delete locked object '{objectName}' ({objId})");
            }

            // Delete the object
            bool success = doc.Objects.Delete(objId, quietDelete);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to delete object '{objectName}' ({objId})");
            }

            return new JObject
            {
                ["status"] = "success",
                ["id"] = objId.ToString(),
                ["name"] = objectName,
                ["type"] = objectType,
                ["deleted"] = true,
            };
        }
    }
}
