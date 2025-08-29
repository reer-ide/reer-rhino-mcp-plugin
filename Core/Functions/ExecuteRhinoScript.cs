using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Runtime;
using ReerRhinoMCPPlugin.Core;
using ReerRhinoMCPPlugin.Core.Common;

namespace ReerRhinoMCPPlugin.Core.Functions
{
    [MCPTool("execute_rhino_script", "Execute RhinoScript/Python code with automatic metadata tracking", ModifiesDocument = true)]
    public class ExecuteRhinoscript : ITool
    {
        public JObject Execute(JObject parameters)
        {
            var doc = RhinoDoc.ActiveDoc;

            string code = parameters["code"]?.ToString();
            if (string.IsNullOrEmpty(code))
            {
                return new JObject
                {
                    ["error"] = "No code provided"
                };
            }

            // Register undo record
            var undoRecordSerialNumber = doc.BeginUndoRecord("Execute Rhino Script");

            try
            {
                // Get object count before execution to track new objects
                var objectsBefore = doc.Objects.Select(obj => obj.Id).ToHashSet();

                // Inject metadata helper function into the code
                string enhancedCode = InjectMetadataHelper(code);

                var output = new StringBuilder();
                
                // Create a new Python script instance
                PythonScript pythonScript = PythonScript.Create();

                pythonScript.Output += (message) =>
                {
                    output.Append(message);
                };

                // Setup the script context with the current document
                if (doc != null)
                    pythonScript.SetupScriptContext(doc);

                // Execute the Python code
                pythonScript.ExecuteScript(enhancedCode);

                // Find new objects created during execution
                var objectsAfter = doc.Objects.Select(obj => obj.Id).ToHashSet();
                var newObjects = objectsAfter.Except(objectsBefore).ToList();

                // End undo record
                doc.EndUndoRecord(undoRecordSerialNumber);

                return new JObject
                {
                    ["status"] = "success",
                    ["message"] = "Code executed successfully",
                    ["printed_output"] = output.ToString(),
                    ["new_objects_count"] = newObjects.Count,
                    ["new_objects"] = new JArray(newObjects.Select(id => id.ToString()))
                };
            }
            catch (Exception ex)
            {
                // End undo record
                doc.EndUndoRecord(undoRecordSerialNumber);
                
                // Undo the changes since execution failed
                doc.Undo();
                
                Logger.Error($"Error executing code: {ex.Message}");
                return new JObject
                {
                    ["status"] = "error",
                    ["error"] = ex.Message
                };
            }
        }

        private string InjectMetadataHelper(string originalCode)
        {
            // Inject the simplified add_rhino_objects_metadata helper function
            string metadataHelper = @"
import rhinoscriptsyntax as rs
import scriptcontext as sc

def add_rhino_objects_metadata(object_ids, name=None, description=None):
    '''Add name and description to Rhino objects'''
    try:
        results = []
        
        # Handle single object ID
        if isinstance(object_ids, str):
            object_ids = [object_ids]
        
        for obj_id in object_ids:
            try:
                # Set name if provided
                if name:
                    rs.ObjectName(obj_id, name)
                
                # Set description as user text if provided
                if description:
                    rs.SetUserText(obj_id, 'description', description)
                
                results.append({
                    'object_id': str(obj_id),
                    'status': 'success',
                    'name': name,
                    'description': description
                })
                
            except Exception as e:
                results.append({
                    'object_id': str(obj_id),
                    'status': 'error',
                    'error': str(e)
                })
        
        return results
    except Exception as e:
        return [{'status': 'error', 'error': str(e)}]

";

            return metadataHelper + "\n" + originalCode;
        }
    }
}