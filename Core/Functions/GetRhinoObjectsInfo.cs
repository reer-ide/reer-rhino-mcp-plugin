using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Serializers;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("get_rhino_objects_info", "Get detailed information about specific objects by their GUIDs, or all objects in the document", RequiresDocument = true)]
    public class GetRhinoObjectsInfo : ITool
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;

                var objGuids = parameters["obj_guids"] as JArray;
                bool getAllObjects = parameters["get_all_objects"]?.ToObject<bool>() ?? false;
                bool includeAttributes = parameters["include_attributes"]?.ToObject<bool>() ?? false;
                
                var objects = new JArray();
                int foundCount = 0;

                if (getAllObjects)
                {
                    // Get all objects in the document
                    foreach (var rhinoObject in doc.Objects)
                    {
                        if (rhinoObject == null || !rhinoObject.IsValid) continue;

                        var objectData = BuildObjectData(rhinoObject, includeAttributes, doc);
                        objects.Add(objectData);
                        foundCount++;
                    }
                }
                else if (objGuids != null && objGuids.Count > 0)
                {
                    // Get specific objects by GUID
                    foreach (var guidToken in objGuids)
                    {
                        if (Guid.TryParse(guidToken.ToString(), out Guid objectGuid))
                        {
                            var rhinoObject = doc.Objects.Find(objectGuid);
                            if (rhinoObject != null && rhinoObject.IsValid)
                            {
                                var objectData = BuildObjectData(rhinoObject, includeAttributes, doc);
                                objects.Add(objectData);
                                foundCount++;
                            }
                        }
                    }
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "Either 'obj_guids' array or 'get_all_objects' = true must be provided"
                    };
                }

                return new JObject
                {
                    ["status"] = "success",
                    ["objects"] = objects,
                    ["count"] = foundCount,
                    ["get_all_objects"] = getAllObjects,
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting objects info: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error getting objects info: {ex.Message}"
                };
            }
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

            // Include all user metadata
            var userStrings = rhinoObject.Attributes.GetUserStrings();
            if (userStrings != null && userStrings.Count > 0)
            {
                var metadata = new JObject();
                foreach (string key in userStrings.AllKeys)
                {
                    metadata[key] = userStrings[key];
                }
                objectData["user_metadata"] = metadata;
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