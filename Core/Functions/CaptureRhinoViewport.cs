using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Core.Functions;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("capture_rhino_viewport", "Capture the current viewport as an image")]
    public class CaptureRhinoViewport : ITool
    {
        private const string ANNOTATION_LAYER = "MCP_Annotations";

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

                var activeView = doc.Views.ActiveView;
                if (activeView == null)
                {
                    return new JObject
                    {
                        ["error"] = "No active viewport"
                    };
                }

                // Parse parameters
                string layerName = parameters["layer"]?.ToString();
                bool showAnnotations = parameters["show_annotations"]?.Value<bool>() ?? true;
                int maxSize = parameters["max_size"]?.Value<int>() ?? 800;

                string originalLayer = doc.Layers.CurrentLayer.Name;
                var tempDots = new List<Guid>();

                try
                {
                    // Add annotations if requested
                    if (showAnnotations)
                    {
                        tempDots = AddAnnotations(doc, layerName);
                    }

                    // Capture viewport to bitmap
                    using (var bitmap = CaptureViewportToBitmap(activeView))
                    {
                        if (bitmap == null)
                        {
                            return new JObject
                            {
                                ["error"] = "Failed to capture viewport"
                            };
                        }

                        // Resize image while maintaining aspect ratio
                        using (var resizedBitmap = ResizeImage(bitmap, maxSize))
                        {
                            // Convert to base64 JPEG
                            string base64Data = ConvertBitmapToBase64(resizedBitmap);

                            return new JObject
                            {
                                ["type"] = "image",
                                ["source"] = new JObject
                                {
                                    ["type"] = "base64",
                                    ["media_type"] = "image/jpeg",
                                    ["data"] = base64Data
                                }
                            };
                        }
                    }
                }
                finally
                {
                    // Clean up temporary annotations
                    if (tempDots.Count > 0)
                    {
                        foreach (var dotId in tempDots)
                        {
                            doc.Objects.Delete(dotId, true);
                        }
                    }

                    // Restore original layer
                    var layer = doc.Layers.FindName(originalLayer);
                    if (layer != null)
                    {
                        doc.Layers.SetCurrentLayerIndex(layer.Index, true);
                    }

                    // Redraw views to remove annotations
                    doc.Views.Redraw();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error capturing viewport: {ex.Message}");
                return new JObject
                {
                    ["type"] = "text",
                    ["text"] = $"Error capturing viewport: {ex.Message}"
                };
            }
        }

        private List<Guid> AddAnnotations(RhinoDoc doc, string layerName)
        {
            var tempDots = new List<Guid>();

            try
            {
                // Ensure annotation layer exists
                Layer annotationLayer = doc.Layers.FindName(ANNOTATION_LAYER);
                if (annotationLayer == null)
                {
                    var newLayer = new Layer
                    {
                        Name = ANNOTATION_LAYER,
                        Color = Color.Red
                    };
                    int layerIndex = doc.Layers.Add(newLayer);
                    annotationLayer = doc.Layers[layerIndex];
                }

                // Set annotation layer as current
                doc.Layers.SetCurrentLayerIndex(annotationLayer.Index, true);

                // Create text dots for objects
                foreach (var rhinoObject in doc.Objects)
                {
                    if (rhinoObject?.IsValid != true) continue;

                    // Filter by layer if specified
                    if (!string.IsNullOrEmpty(layerName))
                    {
                        var objectLayer = doc.Layers[rhinoObject.Attributes.LayerIndex];
                        if (objectLayer?.Name != layerName)
                            continue;
                    }

                    // Get or create short_id
                    string shortId = rhinoObject.Attributes.GetUserString("short_id");
                    if (string.IsNullOrEmpty(shortId))
                    {
                        shortId = DateTime.Now.ToString("ddHHmmss");
                        var attributes = rhinoObject.Attributes.Duplicate();
                        attributes.SetUserString("short_id", shortId);
                        doc.Objects.ModifyAttributes(rhinoObject, attributes, true);
                    }

                    // Get object name
                    string objectName = rhinoObject.Attributes.Name ?? "Unnamed";

                    // Get bounding box and place text dot
                    var bbox = rhinoObject.Geometry?.GetBoundingBox(true);
                    if (bbox?.IsValid == true)
                    {
                        // Use top corner of bounding box
                        var dotPosition = new Point3d(bbox.Value.Max.X, bbox.Value.Max.Y, bbox.Value.Max.Z);
                        string dotText = $"{objectName}\n{shortId}";

                        // Create text dot
                        var textDot = new TextDot(dotText, dotPosition);
                        textDot.FontHeight = 8;

                        var dotId = doc.Objects.AddTextDot(textDot);
                        if (dotId != Guid.Empty)
                        {
                            tempDots.Add(dotId);
                        }
                    }
                }

                // Redraw to show annotations
                doc.Views.Redraw();
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error adding annotations: {ex.Message}");
            }

            return tempDots;
        }

        private Bitmap CaptureViewportToBitmap(RhinoView rhinoView)
        {
            try
            {
                // Capture the view to a bitmap
                var size = rhinoView.ActiveViewport.Size;
                var bitmap = rhinoView.CaptureToBitmap(size);
                return bitmap;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error capturing viewport bitmap: {ex.Message}");
                return null;
            }
        }

        private Bitmap ResizeImage(Bitmap originalBitmap, int maxSize)
        {
            try
            {
                int originalWidth = originalBitmap.Width;
                int originalHeight = originalBitmap.Height;

                // Calculate new dimensions while maintaining aspect ratio
                int newWidth, newHeight;
                if (originalWidth > originalHeight)
                {
                    newWidth = maxSize;
                    newHeight = (int)(originalHeight * ((double)maxSize / originalWidth));
                }
                else
                {
                    newHeight = maxSize;
                    newWidth = (int)(originalWidth * ((double)maxSize / originalHeight));
                }

                // Create resized bitmap
                var resizedBitmap = new Bitmap(newWidth, newHeight);
                using (var graphics = Graphics.FromImage(resizedBitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                }

                return resizedBitmap;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error resizing image: {ex.Message}");
                return null;
            }
        }

        private string ConvertBitmapToBase64(Bitmap bitmap)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    // Save as JPEG
                    bitmap.Save(memoryStream, ImageFormat.Jpeg);
                    
                    // Convert to base64
                    byte[] imageBytes = memoryStream.ToArray();
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error converting bitmap to base64: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
