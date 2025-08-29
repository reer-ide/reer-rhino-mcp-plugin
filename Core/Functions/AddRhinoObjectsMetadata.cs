using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("add_rhino_objects_metadata", "Add name and description to Rhino objects", ModifiesDocument = true)]
    public class AddRhinoObjectMetadata : ITool
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                var objectIds = GetObjectIds(parameters);
                if (objectIds.Length == 0)
                {
                    return new JObject
                    {
                        ["error"] = "No object IDs provided"
                    };
                }

                string name = parameters["name"]?.ToString();
                string description = parameters["description"]?.ToString();

                var results = new JArray();

                foreach (var objectId in objectIds)
                {
                    try
                    {
                        var result = AddMetadataToObject(doc, objectId, name, description);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        results.Add(new JObject
                        {
                            ["object_id"] = objectId.ToString(),
                            ["status"] = "error",
                            ["error"] = ex.Message
                        });
                    }
                }

                return new JObject
                {
                    ["status"] = "success",
                    ["objects_processed"] = results.Count,
                    ["results"] = results
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error adding metadata: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error adding metadata: {ex.Message}"
                };
            }
        }

        private JObject AddMetadataToObject(RhinoDoc doc, Guid objectId, string name, string description)
        {
            var rhinoObject = doc.Objects.Find(objectId);
            if (rhinoObject == null)
            {
                return new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["status"] = "error",
                    ["error"] = "Object not found"
                };
            }

            // Update object attributes
            var attributes = rhinoObject.Attributes.Duplicate();
            
            // Set name if provided
            if (!string.IsNullOrEmpty(name))
            {
                attributes.Name = name;
            }

            // Set description as user text if provided
            if (!string.IsNullOrEmpty(description))
            {
                attributes.SetUserString("description", description);
            }

            // Apply changes
            bool success = doc.Objects.ModifyAttributes(rhinoObject, attributes, true);

            if (success)
            {
                Logger.Success($"Updated object {objectId}: name='{name}', description='{description}'");
                
                return new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["status"] = "success",
                    ["name"] = name ?? rhinoObject.Attributes.Name,
                    ["description"] = description ?? rhinoObject.Attributes.GetUserString("description")
                };
            }
            else
            {
                return new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["status"] = "error",
                    ["error"] = "Failed to modify object attributes"
                };
            }
        }

        private Guid[] GetObjectIds(JObject parameters)
        {
            // Handle single object ID
            if (parameters["object_id"] != null)
            {
                if (Guid.TryParse(parameters["object_id"].ToString(), out Guid singleId))
                {
                    return new Guid[] { singleId };
                }
            }

            // Handle array of object IDs
            if (parameters["object_ids"] is JArray idsArray)
            {
                return idsArray
                    .Where(token => Guid.TryParse(token.ToString(), out _))
                    .Select(token => Guid.Parse(token.ToString()))
                    .ToArray();
            }

            return new Guid[0];
        }
    }
}