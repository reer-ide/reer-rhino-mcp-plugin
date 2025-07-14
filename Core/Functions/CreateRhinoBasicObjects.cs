using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("create_rhino_basic_geometries", "Create basic geometries in Rhino", ModifiesDocument = true)]
    public class CreateRhinoBasicObjects
    {
        private readonly AddRhinoObjectMetadata _metadataHandler;

        public CreateRhinoBasicObjects()
        {
            _metadataHandler = new AddRhinoObjectMetadata();
        }

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

                // Handle both single object and multiple objects input
                JArray objectsToCreate = null;
                
                // Check if we have an "objects" array (new format)
                if (parameters["objects"] is JArray objectsArray)
                {
                    objectsToCreate = objectsArray;
                }
                // Fall back to single object format for backward compatibility
                else if (parameters["geometry_type"] != null)
                {
                    objectsToCreate = new JArray
                    {
                        new JObject
                        {
                            ["type"] = parameters["geometry_type"],
                            ["params"] = parameters["parameters"]
                        }
                    };
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "Missing 'objects' array or 'geometry_type' parameter"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Create Rhino Basic Geometries");

                try
                {
                    var createdObjects = new JArray();
                    var results = new JObject();

                    foreach (var objectToken in objectsToCreate)
                    {
                        var objectParams = objectToken as JObject;
                        if (objectParams == null) continue;

                        try
                        {
                            var result = CreateSingleObject(doc, objectParams);
                            createdObjects.Add(result);
                            
                            // Add to results with object name as key
                            string objectName = result["name"]?.ToString() ?? $"Object_{createdObjects.Count}";
                            results[objectName] = result;
                        }
                        catch (Exception ex)
                        {
                            var errorResult = new JObject
                            {
                                ["status"] = "error",
                                ["error"] = ex.Message,
                                ["type"] = objectParams["type"]?.ToString() ?? "unknown"
                            };
                            createdObjects.Add(errorResult);
                            
                            string objectName = objectParams["name"]?.ToString() ?? $"Error_{createdObjects.Count}";
                            results[objectName] = errorResult;
                        }
                    }

                    // Update views
                    doc.Views.Redraw();

                    // End undo record
                    doc.EndUndoRecord(undoRecordSerialNumber);

                    return new JObject
                    {
                        ["status"] = "success",
                        ["objects_created"] = createdObjects,
                        ["count"] = createdObjects.Count,
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
                RhinoApp.WriteLine($"Error creating geometries: {ex.Message}");
                return new JObject
                {
                    ["error"] = $"Error creating geometries: {ex.Message}"
                };
            }
        }

        private JObject CreateSingleObject(RhinoDoc doc, JObject objectParams)
        {
            string geometryType = objectParams["type"]?.ToString();
            var geoParams = objectParams["params"] as JObject ?? new JObject();
            
            // Copy object-level parameters to params for backward compatibility
            if (objectParams["name"] != null) geoParams["name"] = objectParams["name"];
            if (objectParams["description"] != null) geoParams["description"] = objectParams["description"];
            if (objectParams["color"] != null) geoParams["color"] = objectParams["color"];
            if (objectParams["layer"] != null) geoParams["layer"] = objectParams["layer"];

            if (string.IsNullOrEmpty(geometryType))
            {
                throw new InvalidOperationException("Missing geometry type");
            }

            // Require name for every object
            string name = geoParams["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Name is required for every created object");
            }

            Guid objectId = Guid.Empty;

            // Handle geometry creation
            switch (geometryType.ToLower())
            {
                case "point":
                    objectId = CreatePoint(doc, geoParams);
                    break;
                case "line":
                    objectId = CreateLine(doc, geoParams);
                    break;
                case "polyline":
                    objectId = CreatePolyline(doc, geoParams);
                    break;
                case "circle":
                    objectId = CreateCircle(doc, geoParams);
                    break;
                case "arc":
                    objectId = CreateArc(doc, geoParams);
                    break;
                case "ellipse":
                    objectId = CreateEllipse(doc, geoParams);
                    break;
                case "rectangle":
                    objectId = CreateRectangle(doc, geoParams);
                    break;
                case "polygon":
                    objectId = CreatePolygon(doc, geoParams);
                    break;
                case "box":
                    objectId = CreateBox(doc, geoParams);
                    break;
                case "sphere":
                    objectId = CreateSphere(doc, geoParams);
                    break;
                case "cylinder":
                    objectId = CreateCylinder(doc, geoParams);
                    break;
                case "cone":
                    objectId = CreateCone(doc, geoParams);
                    break;
                case "torus":
                    objectId = CreateTorus(doc, geoParams);
                    break;
                case "plane":
                    objectId = CreatePlane(doc, geoParams);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported geometry type: {geometryType}");
            }

            if (objectId == Guid.Empty)
            {
                throw new InvalidOperationException($"Failed to create {geometryType}");
            }

            // Apply visual attributes (color, layer) first
            ApplyVisualAttributes(doc, objectId, geoParams);

            // Apply metadata using the existing AddRhinoObjectsMetadata function
            var metadataResult = ApplyMetadataUsingExistingFunction(objectId, geoParams);
            
            // Handle transformations
            ApplyTransformations(doc, objectId, geoParams);

            return new JObject
            {
                ["status"] = "success",
                ["object_id"] = objectId.ToString(),
                ["name"] = name,
                ["description"] = geoParams["description"]?.ToString(),
                ["type"] = geometryType,
                ["metadata"] = metadataResult
            };
        }

        private JObject ApplyMetadataUsingExistingFunction(Guid objectId, JObject parameters)
        {
            try
            {
                var metadataParams = new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["name"] = parameters["name"]?.ToString(),
                    ["description"] = parameters["description"]?.ToString()
                };

                return _metadataHandler.Execute(metadataParams);
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["status"] = "error",
                    ["error"] = $"Failed to apply metadata: {ex.Message}"
                };
            }
        }

        private void ApplyVisualAttributes(RhinoDoc doc, Guid objectId, JObject parameters)
        {
            var rhinoObject = doc.Objects.Find(objectId);
            if (rhinoObject == null) return;

            var attributes = rhinoObject.Attributes.Duplicate();

            // Set color if provided
            string colorStr = parameters["color"]?.ToString();
            if (!string.IsNullOrEmpty(colorStr))
            {
                if (TryParseColor(colorStr, out Color color))
                {
                    attributes.ObjectColor = color;
                    attributes.ColorSource = ObjectColorSource.ColorFromObject;
                }
            }

            // Set layer if provided
            string layerName = parameters["layer"]?.ToString();
            if (!string.IsNullOrEmpty(layerName))
            {
                var layer = doc.Layers.FindName(layerName);
                if (layer != null)
                {
                    attributes.LayerIndex = layer.Index;
                }
                else
                {
                    // Create layer if it doesn't exist
                    var newLayer = new Layer { Name = layerName };
                    int layerIndex = doc.Layers.Add(newLayer);
                    attributes.LayerIndex = layerIndex;
                }
            }

            doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
        }

        private void ApplyTransformations(RhinoDoc doc, Guid objectId, JObject parameters)
        {
            var rhinoObject = doc.Objects.Find(objectId);
            if (rhinoObject == null) return;

            Transform transform = Transform.Identity;
            bool needsTransform = false;

            // Apply translation
            if (parameters["translate"] is JArray translateArray && translateArray.Count >= 3)
            {
                var translation = Transform.Translation(
                    GetDoubleFromToken(translateArray[0]),
                    GetDoubleFromToken(translateArray[1]),
                    GetDoubleFromToken(translateArray[2])
                );
                transform = transform * translation;
                needsTransform = true;
            }

            // Apply rotation
            if (parameters["rotate"] is JObject rotateObj)
            {
                var axis = GetPoint3d(rotateObj["axis"] as JArray, new Point3d(0, 0, 1));
                var angle = GetDoubleFromToken(rotateObj["angle"]);
                var center = GetPoint3d(rotateObj["center"] as JArray, Point3d.Origin);
                
                var rotation = Transform.Rotation(angle, new Vector3d(axis), center);
                transform = transform * rotation;
                needsTransform = true;
            }

            // Apply scale
            if (parameters["scale"] != null)
            {
                double scale = GetDoubleFromToken(parameters["scale"]);
                if (scale != 1.0 && scale > 0)
                {
                    var center = GetPoint3d(parameters["scale_center"] as JArray, Point3d.Origin);
                    var scaling = Transform.Scale(center, scale);
                    transform = transform * scaling;
                    needsTransform = true;
                }
            }

            if (needsTransform)
            {
                doc.Objects.Transform(rhinoObject, transform, true);
            }
        }

        private bool TryParseColor(string colorStr, out Color color)
        {
            color = Color.Black;
            
            // Try hex format (#RRGGBB)
            if (colorStr.StartsWith("#") && colorStr.Length == 7)
            {
                try
                {
                    color = ColorTranslator.FromHtml(colorStr);
                    return true;
                }
                catch { }
            }
            
            // Try RGB format (r,g,b)
            if (colorStr.Contains(","))
            {
                var parts = colorStr.Split(',');
                if (parts.Length == 3)
                {
                    if (int.TryParse(parts[0].Trim(), out int r) && 
                        int.TryParse(parts[1].Trim(), out int g) && 
                        int.TryParse(parts[2].Trim(), out int b))
                    {
                        color = Color.FromArgb(r, g, b);
                        return true;
                    }
                }
            }
            
            // Try named color
            try
            {
                color = Color.FromName(colorStr);
                return color.IsKnownColor;
            }
            catch { }
            
            return false;
        }

        private Guid CreatePoint(RhinoDoc doc, JObject parameters)
        {
            double x = GetDoubleValue(parameters, "x", 0);
            double y = GetDoubleValue(parameters, "y", 0);
            double z = GetDoubleValue(parameters, "z", 0);
            return doc.Objects.AddPoint(x, y, z);
        }

        private Guid CreateLine(RhinoDoc doc, JObject parameters)
        {
            var start = GetPoint3d(parameters, "start");
            var end = GetPoint3d(parameters, "end");
            return doc.Objects.AddLine(start, end);
        }

        private Guid CreatePolyline(RhinoDoc doc, JObject parameters)
        {
            var points = GetPoint3dList(parameters, "points");
            return doc.Objects.AddPolyline(points);
        }

        private Guid CreateCircle(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = GetDoubleValue(parameters, "radius", 1.0);
            var circle = new Circle(center, radius);
            return doc.Objects.AddCircle(circle);
        }

        private Guid CreateArc(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = GetDoubleValue(parameters, "radius", 1.0);
            double angle = GetDoubleValue(parameters, "angle", 90.0);
            var arc = new Arc(new Plane(center, Vector3d.ZAxis), radius, angle * Math.PI / 180);
            return doc.Objects.AddArc(arc);
        }

        private Guid CreateEllipse(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radiusX = GetDoubleValue(parameters, "radius_x", 1.0);
            double radiusY = GetDoubleValue(parameters, "radius_y", 0.5);
            var ellipse = new Ellipse(new Plane(center, Vector3d.ZAxis), radiusX, radiusY);
            return doc.Objects.AddEllipse(ellipse);
        }

        private Guid CreateCurve(RhinoDoc doc, JObject parameters)
        {
            var points = GetPoint3dList(parameters, "points");
            int degree = GetIntValue(parameters, "degree", 3);
            var curve = Curve.CreateControlPointCurve(points, degree);
            if (curve == null)
                throw new InvalidOperationException("Unable to create control point curve from given points");
            return doc.Objects.AddCurve(curve);
        }

        private Guid CreateBox(RhinoDoc doc, JObject parameters)
        {
            double width = GetDoubleValue(parameters, "width", 1.0);
            double length = GetDoubleValue(parameters, "length", 1.0);
            double height = GetDoubleValue(parameters, "height", 1.0);
            
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            var plane = new Plane(center, Vector3d.ZAxis);
            
            var box = new Box(
                plane,
                new Interval(-width / 2, width / 2),
                new Interval(-length / 2, length / 2),
                new Interval(-height / 2, height / 2)
            );
            return doc.Objects.AddBox(box);
        }

        private Guid CreateSphere(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = GetDoubleValue(parameters, "radius", 1.0);
            var sphere = new Sphere(center, radius);
            return doc.Objects.AddBrep(sphere.ToBrep());
        }

        private Guid CreateCone(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = GetDoubleValue(parameters, "radius", 1.0);
            double height = GetDoubleValue(parameters, "height", 2.0);
            bool cap = GetBoolValue(parameters, "cap", true);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var cone = new Cone(plane, height, radius);
            var brep = Brep.CreateFromCone(cone, cap);
            return doc.Objects.AddBrep(brep);
        }

        private Guid CreateCylinder(RhinoDoc doc, JObject parameters)
        {
            var center = GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = GetDoubleValue(parameters, "radius", 1.0);
            double height = GetDoubleValue(parameters, "height", 2.0);
            bool cap = GetBoolValue(parameters, "cap", true);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var circle = new Circle(plane, radius);
            var cylinder = new Cylinder(circle, height);
            return doc.Objects.AddBrep(cylinder.ToBrep(cap, cap));
        }

        private Guid CreateSurface(RhinoDoc doc, JObject parameters)
        {
            var points = GetPoint3dList(parameters, "points");
            var count = GetIntArray(parameters, "count", new int[] { 2, 2 });
            var degree = GetIntArray(parameters, "degree", new int[] { 3, 3 });
            var closed = GetBoolArray(parameters, "closed", new bool[] { false, false });
            
            var surface = NurbsSurface.CreateThroughPoints(
                points, count[0], count[1], degree[0], degree[1], closed[0], closed[1]);
            
            if (surface == null)
                throw new InvalidOperationException("Unable to create surface from given points");
                
            return doc.Objects.AddSurface(surface);
        }

        private void ApplyVisualAttributes(RhinoDoc doc, Guid objectId, JObject parameters)
        {
            var rhinoObject = doc.Objects.Find(objectId);
            if (rhinoObject == null) return;

            var attributes = rhinoObject.Attributes.Duplicate();
            bool modified = false;

            // Apply color
            if (parameters["color"] != null)
            {
                var colorArray = GetIntArray(parameters, "color", null);
                if (colorArray != null && colorArray.Length >= 3)
                {
                    attributes.ColorSource = ObjectColorSource.ColorFromObject;
                    attributes.ObjectColor = Color.FromArgb(colorArray[0], colorArray[1], colorArray[2]);
                    modified = true;
                }
            }

            // Apply layer
            string layerName = parameters["layer"]?.ToString();
            if (!string.IsNullOrEmpty(layerName))
            {
                var layer = doc.Layers.FindName(layerName);
                if (layer != null)
                {
                    attributes.LayerIndex = layer.Index;
                    modified = true;
                }
            }

            if (modified)
            {
                doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
            }
        }

        // Helper methods for parameter extraction
        private double GetDoubleValue(JObject parameters, string key, double defaultValue = 0)
        {
            var token = parameters[key];
            if (token != null && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return defaultValue;
        }

        private int GetIntValue(JObject parameters, string key, int defaultValue = 0)
        {
            var token = parameters[key];
            if (token != null && int.TryParse(token.ToString(), out int result))
                return result;
            return defaultValue;
        }

        private bool GetBoolValue(JObject parameters, string key, bool defaultValue = false)
        {
            var token = parameters[key];
            if (token != null && bool.TryParse(token.ToString(), out bool result))
                return result;
            return defaultValue;
        }

        private Point3d GetPoint3d(JObject parameters, string key, Point3d defaultValue = default)
        {
            var token = parameters[key] as JArray;
            if (token != null && token.Count >= 3)
            {
                double x = GetDoubleFromToken(token[0]);
                double y = GetDoubleFromToken(token[1]);
                double z = GetDoubleFromToken(token[2]);
                return new Point3d(x, y, z);
            }
            return defaultValue;
        }

        private List<Point3d> GetPoint3dList(JObject parameters, string key)
        {
            var points = new List<Point3d>();
            var token = parameters[key] as JArray;
            if (token != null)
            {
                foreach (var pointToken in token)
                {
                    var pointArray = pointToken as JArray;
                    if (pointArray != null && pointArray.Count >= 3)
                    {
                        double x = GetDoubleFromToken(pointArray[0]);
                        double y = GetDoubleFromToken(pointArray[1]);
                        double z = GetDoubleFromToken(pointArray[2]);
                        points.Add(new Point3d(x, y, z));
                    }
                }
            }
            return points;
        }

        private int[] GetIntArray(JObject parameters, string key, int[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new int[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = GetIntFromToken(token[i]);
                }
                return result;
            }
            return defaultValue;
        }

        private bool[] GetBoolArray(JObject parameters, string key, bool[] defaultValue = null)
        {
            var token = parameters[key] as JArray;
            if (token != null)
            {
                var result = new bool[token.Count];
                for (int i = 0; i < token.Count; i++)
                {
                    result[i] = GetBoolFromToken(token[i]);
                }
                return result;
            }
            return defaultValue;
        }

        private double GetDoubleFromToken(JToken token)
        {
            if (token != null && double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return 0;
        }

        private int GetIntFromToken(JToken token)
        {
            if (token != null && int.TryParse(token.ToString(), out int result))
                return result;
            return 0;
        }

        private bool GetBoolFromToken(JToken token)
        {
            if (token != null && bool.TryParse(token.ToString(), out bool result))
                return result;
            return false;
        }
    }
}