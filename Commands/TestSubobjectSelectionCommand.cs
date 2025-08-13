using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input.Custom;
using ReerRhinoMCPPlugin.Core.Common;
using ReerRhinoMCPPlugin.Core.Functions;
using Newtonsoft.Json.Linq;

namespace ReerRhinoMCPPlugin.Commands
{
    public class TestSubobjectSelectionCommand : Command
    {
        public TestSubobjectSelectionCommand()
        {
            Instance = this;
        }

        public static TestSubobjectSelectionCommand Instance { get; private set; }

        public override string EnglishName => "TestSubobjectSelection";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                Logger.Info("========================================");
                Logger.Info("Testing GetRhinoSelectedObjects function");
                Logger.Info("========================================");
                
                // Create an instance of the GetRhinoSelectedObjects function
                var getSelectedFunction = new GetRhinoSelectedObjects();
                
                // Test: Get current selection (handles both full objects and subobjects automatically)
                Logger.Info("\nTesting GetRhinoSelectedObjects:");
                Logger.Info("   (First select some objects or subobjects, then press Enter)");
                
                var parameters = new JObject
                {
                    ["include_lights"] = true,  // Include lights in selection
                    ["include_grips"] = true    // Include objects with grips on
                };
                
                var result = getSelectedFunction.Execute(parameters);
                
                if (result["error"] != null)
                {
                    Logger.Error($"   Error: {result["error"]}");
                }
                else
                {
                    Logger.Success($"   Status: {result["status"]}");
                    Logger.Info($"   Total selected count: {result["selected_count"]} (includes subobjects)");
                    Logger.Info($"   Unique objects count: {result["unique_objects_count"]}");
                    Logger.Info($"   Include lights: {result["include_lights"]}");
                    Logger.Info($"   Include grips: {result["include_grips"]}");
                    Logger.Info($"   Objects in file: {result["object_count_in_file"]}");
                    
                    var selectedObjects = result["selected_objects"] as JArray;
                    if (selectedObjects != null)
                    {
                        Logger.Info($"\n   Selected Objects Details:");
                        foreach (JObject obj in selectedObjects)
                        {
                            Logger.Info($"   ----------------------------------------");
                            Logger.Info($"   Object ID: {obj["id"]}");
                            Logger.Info($"   Type: {obj["type"]}");
                            Logger.Info($"   Name: {obj["name"] ?? "Unnamed"}");
                            Logger.Info($"   Layer: {obj["layer"]}");
                            Logger.Info($"   Selection Type: {obj["selection_type"]}");
                            
                            if (obj["selection_type"]?.ToString() == "subobject" && obj["subobjects"] is JArray subobjects)
                            {
                                Logger.Info($"   Subobjects ({subobjects.Count}):");
                                foreach (JObject subobj in subobjects)
                                {
                                    Logger.Info($"     - {subobj["type"]} [Index: {subobj["index"]}]");
                                }
                            }
                            
                            if (obj["metadata"] is JObject metadata && metadata.Count > 0)
                            {
                                Logger.Info($"   Metadata:");
                                foreach (var prop in metadata.Properties())
                                {
                                    Logger.Info($"     {prop.Name}: {prop.Value}");
                                }
                            }
                        }
                    }
                    else
                    {
                        Logger.Info("   No objects selected");
                    }
                }
                
                Logger.Info("\n========================================");
                Logger.Success("Test completed successfully!");
                Logger.Info("========================================");
                
                return Result.Success;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TestSubobjectSelection: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                return Result.Failure;
            }
        }
    }
}