using System;
using System.Collections.Generic;
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
                
                // Parse parameters
                int maxSize = parameters["max_size"]?.Value<int>() ?? 2048;
                bool includeAnnotations = parameters["include_annotations"]?.Value<bool>() ?? false;
                
                // Clean up old annotations if requested
                if (!includeAnnotations)
                {
                    CleanUpAnnotations(doc);
                }
                
                Logger.Info($"Capturing viewport with max_size: {maxSize}, include_annotations: {includeAnnotations}");

                try
                {
                    // For Mac, viewport capture requires special handling
                    // This is a simplified implementation - in production, you would use
                    // platform-specific image handling or wait for RhinoCommon updates
                    
                    Logger.Warning("Viewport capture on Mac is currently limited. Returning placeholder.");
                    
                    // Return a minimal valid response
                    return new JObject
                    {
                        ["image_data"] = "", // Empty base64 string
                        ["format"] = "png",
                        ["width"] = maxSize,
                        ["height"] = maxSize,
                        ["message"] = "Viewport capture on Mac requires platform-specific implementation"
                    };
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error during viewport capture: {ex.Message}");
                    Logger.Error($"Stack trace: {ex.StackTrace}");
                    return new JObject
                    {
                        ["error"] = $"Capture failed: {ex.Message}"
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in CaptureRhinoViewport: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                return new JObject
                {
                    ["error"] = $"Failed to capture viewport: {ex.Message}"
                };
            }
        }

        private void CleanUpAnnotations(RhinoDoc doc)
        {
            try
            {
                // Clean up any existing annotation layers
                var annotationLayer = doc.Layers.FindName(ANNOTATION_LAYER);
                if (annotationLayer != null)
                {
                    var objectsOnLayer = doc.Objects.FindByLayer(annotationLayer);
                    foreach (var obj in objectsOnLayer)
                    {
                        doc.Objects.Delete(obj, true);
                    }
                    doc.Layers.Delete(annotationLayer.Index, true);
                    Logger.Info($"Cleaned up annotation layer: {ANNOTATION_LAYER}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error cleaning up annotations: {ex.Message}");
            }
        }

        public string GetName() => "capture_rhino_viewport";
    }
}