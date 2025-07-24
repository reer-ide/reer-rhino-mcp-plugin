using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using ReerRhinoMCPPlugin.Core.Functions;
using ReerRhinoMCPPlugin.Core.Common;

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
                    Logger.Error("No active viewport found");
                    return new JObject
                    {
                        ["error"] = "No active viewport"
                    };
                }

                Logger.Info($"Active view found: {activeView.ActiveViewport.Name}");
                
                // Use the official sample approach - work directly with the active view
                // instead of trying to change view focus during capture

                // Parse parameters
                string layerName = parameters["layer"]?.ToString();
                bool showAnnotations = parameters["show_annotations"]?.Value<bool>() ?? true;
                int maxSize = parameters["max_size"]?.Value<int>() ?? 800;
                
                Logger.Info($"Capture parameters - Layer: {layerName ?? "All"}, Annotations: {showAnnotations}, MaxSize: {maxSize}");

                string originalLayer = doc.Layers.CurrentLayer.Name;
                var tempDots = new List<Guid>();

                try
                {
                    // Add annotations if requested
                    if (showAnnotations)
                    {
                        tempDots = AddAnnotations(doc, layerName);
                    }

                    // Capture viewport to bitmap using official sample approach
                    using (var bitmap = CaptureViewportToBitmap(doc))
                    {
                        if (bitmap == null)
                        {
                            return new JObject
                            {
                                ["error"] = "Failed to capture viewport: ViewCapture returned null"
                            };
                        }

                        // Resize image while maintaining aspect ratio
                        using (var resizedBitmap = ResizeImage(bitmap, maxSize))
                        {
                            if (resizedBitmap == null)
                            {
                                return new JObject
                                {
                                    ["error"] = "Failed to resize captured image"
                                };
                            }

                            // Convert to base64 JPEG
                            string base64Data = ConvertBitmapToBase64(resizedBitmap);
                            
                            if (string.IsNullOrEmpty(base64Data))
                            {
                                return new JObject
                                {
                                    ["error"] = "Failed to convert image to base64"
                                };
                            }

                            return new JObject
                            {
                                ["type"] = "image",
                                ["source"] = new JObject
                                {
                                    ["type"] = "base64",
                                    ["media_type"] = "image/png",
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
                Logger.Error($"Error capturing viewport: {ex.Message}");
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

                    // Get object name
                    string objectName = rhinoObject.Attributes.Name ?? "Unnamed";

                    // Get bounding box and place text dot
                    var bbox = rhinoObject.Geometry?.GetBoundingBox(true);
                    if (bbox?.IsValid == true)
                    {
                        // Use top corner of bounding box
                        var dotPosition = new Point3d(bbox.Value.Max.X, bbox.Value.Max.Y, bbox.Value.Max.Z);
                        string dotText = $"{objectName}";

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
                Logger.Error($"Error adding annotations: {ex.Message}");
            }

            return tempDots;
        }
        
        private Bitmap CaptureViewportToBitmap(RhinoDoc doc)
        {
            Bitmap result = null;
            
            // ViewCapture operations need to run on the main UI thread
            if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
            {
                Logger.Info("Invoking ViewCapture on main UI thread...");
                
                // Use manual reset event to wait for completion
                using (var resetEvent = new System.Threading.ManualResetEventSlim(false))
                {
                    var captureAction = new System.Action(() =>
                    {
                        try
                        {
                            result = PerformViewCapture(doc);
                        }
                        finally
                        {
                            resetEvent.Set();
                        }
                    });
                    
                    RhinoApp.InvokeOnUiThread(captureAction);
                    
                    // Wait for completion (with timeout)
                    bool completed = resetEvent.Wait(10000); // 10 second timeout
                    
                    if (!completed)
                    {
                        Logger.Error("UI thread invocation timed out");
                        return null;
                    }
                }
                
                if (result != null)
                {
                    Logger.Success($"UI thread invocation succeeded, bitmap: {result.Width}x{result.Height}");
                }
                else
                {
                    Logger.Error("UI thread invocation completed but result is null");
                }
            }
            else
            {
                Logger.Info("Already on UI thread, performing ViewCapture directly...");
                result = PerformViewCapture(doc);
            }
            
            return result;
        }
        
        private Bitmap PerformViewCapture(RhinoDoc doc)
        {
            try
            {
                RhinoView view = doc.Views.ActiveView;
                if (view == null)
                {
                    Logger.Error("No active view found");
                    return null;
                }

                Logger.Info($"Active viewport: {view.ActiveViewport.Name}");

                ViewCapture viewCapture = new ViewCapture
                {
                    Width = view.ActiveViewport.Size.Width,
                    Height = view.ActiveViewport.Size.Height,
                    ScaleScreenItems = false,
                    DrawAxes = true,
                    DrawGrid = true,
                    DrawGridAxes = true,
                    TransparentBackground = false
                };
                
                System.Drawing.Bitmap bitmap = viewCapture.CaptureToBitmap(view);
                
                if (bitmap != null)
                {
                    Logger.Success($"ViewCapture succeeded: {bitmap.Width}x{bitmap.Height}");
                }
                else
                {
                    Logger.Error("ViewCapture.CaptureToBitmap returned null");
                }
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.Error($"ViewCapture exception: {ex.Message}");
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
                Logger.Error($"Error resizing image: {ex.Message}");
                return null;
            }
        }

        private string ConvertBitmapToBase64(Bitmap bitmap)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    // Save as PNG to preserve transparency if TransparentBackground=true
                    bitmap.Save(memoryStream, ImageFormat.Png);
                    
                    // Convert to base64
                    byte[] imageBytes = memoryStream.ToArray();
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error converting bitmap to base64: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
