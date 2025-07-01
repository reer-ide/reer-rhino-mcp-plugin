using System;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Functions;

namespace ReerRhinoMCPPlugin.Functions.Commands
{
    /// <summary>
    /// Command to create a cube/box object in Rhino
    /// </summary>
    [MCPCommand("create_cube", "Create a cube/box object in Rhino", ModifiesDocument = true)]
    public class CreateCubeCommand : ICommand
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
                double width = GetDoubleParameter(parameters, "width", 1.0);
                double length = GetDoubleParameter(parameters, "length", width); // Default to width if not specified
                double height = GetDoubleParameter(parameters, "height", width); // Default to width if not specified
                
                // Get position (default to origin)
                Point3d position = Point3d.Origin;
                if (parameters.ContainsKey("position"))
                {
                    var posArray = parameters["position"] as JArray;
                    if (posArray != null && posArray.Count >= 3)
                    {
                        position = new Point3d(
                            (double)posArray[0],
                            (double)posArray[1], 
                            (double)posArray[2]
                        );
                    }
                }

                // Get optional name
                string objectName = parameters["name"]?.ToString() ?? "Cube";

                // Create the box geometry
                Box box = new Box(
                    Plane.WorldXY,
                    new Interval(position.X, position.X + width),
                    new Interval(position.Y, position.Y + length),
                    new Interval(position.Z, position.Z + height)
                );

                Brep boxBrep = box.ToBrep();
                if (boxBrep == null)
                {
                    throw new Exception("Failed to create box geometry");
                }

                // Add to document
                Guid objectId = doc.Objects.AddBrep(boxBrep);
                if (objectId == Guid.Empty)
                {
                    throw new Exception("Failed to add cube to document");
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
                    ["type"] = "BOX",
                    ["dimensions"] = new JObject
                    {
                        ["width"] = width,
                        ["length"] = length,
                        ["height"] = height
                    },
                    ["position"] = new JArray { position.X, position.Y, position.Z },
                    ["layer"] = doc.Layers[createdObject.Attributes.LayerIndex].Name,
                    ["message"] = "Cube created successfully"
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error creating cube: {ex.Message}");
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