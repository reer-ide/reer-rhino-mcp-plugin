using System;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Command to create a sphere object in Rhino
    /// </summary>
    [MCPCommand("create_sphere", "Create a sphere object in Rhino", ModifiesDocument = true)]
    public class CreateSphereCommand : ICommand
    {
        public JObject Execute(JObject parameters)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null)
                {
                    throw new Exception("No active document");
                }

                // Parse parameters with defaults
                double radius = GetDoubleParameter(parameters, "radius", 1.0);
                
                // Get center position (default to origin)
                Point3d center = Point3d.Origin;
                if (parameters.ContainsKey("center"))
                {
                    var centerArray = parameters["center"] as JArray;
                    if (centerArray != null && centerArray.Count >= 3)
                    {
                        center = new Point3d(
                            (double)centerArray[0],
                            (double)centerArray[1], 
                            (double)centerArray[2]
                        );
                    }
                }

                // Get optional name
                string objectName = parameters["name"]?.ToString() ?? "Sphere";

                // Create the sphere geometry
                Sphere sphere = new Sphere(center, radius);
                Brep sphereBrep = sphere.ToBrep();
                
                if (sphereBrep == null)
                {
                    throw new Exception("Failed to create sphere geometry");
                }

                // Add to document
                Guid objectId = doc.Objects.AddBrep(sphereBrep);
                if (objectId == Guid.Empty)
                {
                    throw new Exception("Failed to add sphere to document");
                }

                // Set object name if provided
                if (!string.IsNullOrEmpty(objectName))
                {
                    var rhinoObject = doc.Objects.FindId(objectId);
                    if (rhinoObject != null)
                    {
                        rhinoObject.Attributes.Name = objectName;
                        rhinoObject.CommitChanges();
                    }
                }

                // Get the created object info
                var createdObject = doc.Objects.FindId(objectId);
                if (createdObject == null)
                {
                    throw new Exception("Created object not found");
                }

                // Redraw views
                doc.Views.Redraw();

                return new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["name"] = objectName,
                    ["type"] = "SPHERE",
                    ["radius"] = radius,
                    ["center"] = new JArray { center.X, center.Y, center.Z },
                    ["layer"] = doc.Layers[createdObject.Attributes.LayerIndex].Name,
                    ["message"] = "Sphere created successfully"
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating sphere: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to safely extract double parameters
        /// </summary>
        private double GetDoubleParameter(JObject parameters, string key, double defaultValue)
        {
            if (parameters.ContainsKey(key))
            {
                if (double.TryParse(parameters[key]?.ToString(), out double value))
                {
                    return value;
                }
            }
            return defaultValue;
        }
    }
} 