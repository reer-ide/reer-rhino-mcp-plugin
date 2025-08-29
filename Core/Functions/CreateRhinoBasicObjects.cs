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
                // Handle both single object and multiple objects input
                JArray objectsToCreate = null;
                
                // Check if we have an "objects" array (new format)
                if (parameters["objects"] is JArray objectsArray)
                {
                    objectsToCreate = objectsArray;
                }
                else
                {
                    return new JObject
                    {
                        ["error"] = "Missing 'objects' parameter"
                    };
                }

                // Begin undo record
                var undoRecordSerialNumber = doc.BeginUndoRecord("Create Rhino Basic Geometries");

                try
                {
                    var createdObjects = new JArray();
                    var hasErrors = false;

                    foreach (var objectToken in objectsToCreate)
                    {
                        var objectParams = objectToken as JObject;
                        if (objectParams == null) continue;

                        try
                        {
                            var result = CreateSingleObject(doc, objectParams);
                            createdObjects.Add(result);
                        }
                        catch (Exception ex)
                        {
                            hasErrors = true;
                            var errorResult = new JObject
                            {
                                ["name"] = objectParams["name"]?.ToString() ?? "unnamed",
                                ["type"] = objectParams["type"]?.ToString() ?? "unknown",
                                ["error"] = ex.Message
                            };
                            createdObjects.Add(errorResult);
                        }
                    }

                    // Update views
                    doc.Views.Redraw();

                    // End undo record
                    doc.EndUndoRecord(undoRecordSerialNumber);

                    var successCount = createdObjects.Count(obj => obj["error"] == null);
                    var errorCount = createdObjects.Count - successCount;

                    return new JObject
                    {
                        ["status"] = hasErrors ? "partial_success" : "success",
                        ["count"] = createdObjects.Count,
                        ["created"] = successCount,
                        ["errors"] = errorCount,
                        ["objects"] = createdObjects
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
                case "curve":
                    objectId = CreateCurve(doc, geoParams);
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
            var metadataApplied = ApplyMetadataUsingExistingFunction(objectId, geoParams);

            return new JObject
            {
                ["object_id"] = objectId.ToString(),
                ["name"] = name,
                ["type"] = geometryType.ToUpperInvariant(),
                ["description"] = geoParams["description"]?.ToString(),
                ["metadata_applied"] = metadataApplied
            };
        }

        private bool ApplyMetadataUsingExistingFunction(Guid objectId, JObject parameters)
        {
            try
            {
                var metadataParams = new JObject
                {
                    ["object_id"] = objectId.ToString(),
                    ["name"] = parameters["name"]?.ToString(),
                    ["description"] = parameters["description"]?.ToString()
                };

                var result = _metadataHandler.Execute(metadataParams);
                return result["status"]?.ToString() == "success";
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to apply metadata: {ex.Message}");
                return false;
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
    }
}