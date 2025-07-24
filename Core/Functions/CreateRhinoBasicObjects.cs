using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("create_rhino_basic_geometries", "Create basic geometries in Rhino", ModifiesDocument = true)]
    public class CreateRhinoBasicObjects : ITool
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
                Logger.Error($"Error creating geometries: {ex.Message}");
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
                if (ParameterUtils.TryParseColor(colorStr, out Color color))
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
            var translation = ParameterUtils.GetTranslationTransform(parameters, "translate");
            if (!translation.Equals(Transform.Identity))
            {
                transform = transform * translation;
                needsTransform = true;
            }

            // Apply rotation
            if (parameters["rotate"] is JObject rotateObj)
            {
                var axis = ParameterUtils.GetPoint3dFromArray(rotateObj["axis"] as JArray, new Point3d(0, 0, 1));
                var angle = ParameterUtils.GetDoubleFromToken(rotateObj["angle"]);
                var center = ParameterUtils.GetPoint3dFromArray(rotateObj["center"] as JArray, Point3d.Origin);
                
                var rotation = Transform.Rotation(angle, new Vector3d(axis), center);
                transform = transform * rotation;
                needsTransform = true;
            }

            // Apply scale
            if (parameters["scale"] != null)
            {
                var center = ParameterUtils.GetPoint3dFromArray(parameters["scale_center"] as JArray, Point3d.Origin);
                var scaling = ParameterUtils.GetScaleTransform(parameters, center, "scale");
                if (!scaling.Equals(Transform.Identity))
                {
                    transform = transform * scaling;
                    needsTransform = true;
                }
            }

            if (needsTransform)
            {
                doc.Objects.Transform(rhinoObject, transform, true);
            }
        }



        private Guid CreatePoint(RhinoDoc doc, JObject parameters)
        {
            double x = ParameterUtils.GetDoubleValue(parameters, "x", 0);
            double y = ParameterUtils.GetDoubleValue(parameters, "y", 0);
            double z = ParameterUtils.GetDoubleValue(parameters, "z", 0);
            return doc.Objects.AddPoint(x, y, z);
        }

        private Guid CreateLine(RhinoDoc doc, JObject parameters)
        {
            var start = ParameterUtils.GetPoint3d(parameters, "start");
            var end = ParameterUtils.GetPoint3d(parameters, "end");
            return doc.Objects.AddLine(start, end);
        }

        private Guid CreatePolyline(RhinoDoc doc, JObject parameters)
        {
            var points = ParameterUtils.GetPoint3dList(parameters, "points");
            return doc.Objects.AddPolyline(points);
        }

        private Guid CreateCircle(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            var circle = new Circle(center, radius);
            return doc.Objects.AddCircle(circle);
        }

        private Guid CreateArc(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            double angle = ParameterUtils.GetDoubleValue(parameters, "angle", 90.0);
            var arc = new Arc(new Plane(center, Vector3d.ZAxis), radius, angle * Math.PI / 180);
            return doc.Objects.AddArc(arc);
        }

        private Guid CreateEllipse(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radiusX = ParameterUtils.GetDoubleValue(parameters, "radius_x", 1.0);
            double radiusY = ParameterUtils.GetDoubleValue(parameters, "radius_y", 0.5);
            var ellipse = new Ellipse(new Plane(center, Vector3d.ZAxis), radiusX, radiusY);
            return doc.Objects.AddEllipse(ellipse);
        }

        private Guid CreateRectangle(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double width = ParameterUtils.GetDoubleValue(parameters, "width", 2.0);
            double height = ParameterUtils.GetDoubleValue(parameters, "height", 1.0);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var rect = new Rectangle3d(plane, new Interval(-width/2, width/2), new Interval(-height/2, height/2));
            return doc.Objects.AddCurve(rect.ToPolyline().ToPolylineCurve());
        }

        private Guid CreatePolygon(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            int sides = ParameterUtils.GetIntValue(parameters, "sides", 6);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var polygon = Polyline.CreateInscribedPolygon(new Circle(plane, radius), sides);
            polygon.Add(polygon[0]); // Close the polygon
            return doc.Objects.AddPolyline(polygon);
        }

        private Guid CreateTorus(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double majorRadius = ParameterUtils.GetDoubleValue(parameters, "major_radius", 2.0);
            double minorRadius = ParameterUtils.GetDoubleValue(parameters, "minor_radius", 0.5);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var torus = new Torus(plane, majorRadius, minorRadius);
            var brep = torus.ToRevSurface().ToBrep();
            return doc.Objects.AddBrep(brep);
        }

        private Guid CreatePlane(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double width = ParameterUtils.GetDoubleValue(parameters, "width", 2.0);
            double height = ParameterUtils.GetDoubleValue(parameters, "height", 2.0);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var interval = new Interval(-width/2, width/2);
            var intervalY = new Interval(-height/2, height/2);
            var surface = new PlaneSurface(plane, interval, intervalY);
            return doc.Objects.AddSurface(surface);
        }

        private Guid CreateCurve(RhinoDoc doc, JObject parameters)
        {
            var points = ParameterUtils.GetPoint3dList(parameters, "points");
            int degree = ParameterUtils.GetIntValue(parameters, "degree", 3);
            var curve = Curve.CreateControlPointCurve(points, degree);
            if (curve == null)
                throw new InvalidOperationException("Unable to create control point curve from given points");
            return doc.Objects.AddCurve(curve);
        }

        private Guid CreateBox(RhinoDoc doc, JObject parameters)
        {
            double width = ParameterUtils.GetDoubleValue(parameters, "width", 1.0);
            double length = ParameterUtils.GetDoubleValue(parameters, "length", 1.0);
            double height = ParameterUtils.GetDoubleValue(parameters, "height", 1.0);
            
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
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
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            var sphere = new Sphere(center, radius);
            return doc.Objects.AddBrep(sphere.ToBrep());
        }

        private Guid CreateCone(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            double height = ParameterUtils.GetDoubleValue(parameters, "height", 2.0);
            bool cap = ParameterUtils.GetBoolValue(parameters, "cap", true);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var cone = new Cone(plane, height, radius);
            var brep = Brep.CreateFromCone(cone, cap);
            return doc.Objects.AddBrep(brep);
        }

        private Guid CreateCylinder(RhinoDoc doc, JObject parameters)
        {
            var center = ParameterUtils.GetPoint3d(parameters, "center", Point3d.Origin);
            double radius = ParameterUtils.GetDoubleValue(parameters, "radius", 1.0);
            double height = ParameterUtils.GetDoubleValue(parameters, "height", 2.0);
            bool cap = ParameterUtils.GetBoolValue(parameters, "cap", true);
            
            var plane = new Plane(center, Vector3d.ZAxis);
            var circle = new Circle(plane, radius);
            var cylinder = new Cylinder(circle, height);
            return doc.Objects.AddBrep(cylinder.ToBrep(cap, cap));
        }

        private Guid CreateSurface(RhinoDoc doc, JObject parameters)
        {
            var points = ParameterUtils.GetPoint3dList(parameters, "points");
            var count = ParameterUtils.GetIntArray(parameters, "count", new int[] { 2, 2 });
            var degree = ParameterUtils.GetIntArray(parameters, "degree", new int[] { 3, 3 });
            var closed = ParameterUtils.GetBoolArray(parameters, "closed", new bool[] { false, false });
            
            var surface = NurbsSurface.CreateThroughPoints(
                points, count[0], count[1], degree[0], degree[1], closed[0], closed[1]);
            
            if (surface == null)
                throw new InvalidOperationException("Unable to create surface from given points");
                
            return doc.Objects.AddSurface(surface);
        }
    }
}