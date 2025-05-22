---
description:
globs:
alwaysApply: false
---
# Model Context Protocol (MCP)

The Model Context Protocol (MCP) is a standard for communication between AI assistants and external applications. This project implements MCP for Rhino.

## Protocol Structure

MCP commands follow this general structure:

```json
{
  "type": "command_name",
  "params": {
    "param1": "value1",
    "param2": "value2"
  }
}
```

Responses follow this structure:

```json
{
  "status": "success|error",
  "result": {
    // Command-specific result data
  },
  "message": "Error message when status is error"
}
```

## Rhino MCP Commands

The plugin will implement these MCP commands from the original Python implementation:

1. `get_rhino_scene_info()`: Get information about the current Rhino scene
2. `get_rhino_layers()`: Get all layers in the document
3. `execute_code(code)`: Execute Python code in Rhino
4. `get_rhino_objects_with_metadata(filters, metadata_fields)`: Get objects with optional filtering
5. `capture_rhino_viewport(layer, show_annotations, max_size)`: Capture viewport as image
6. `get_rhino_selected_objects(include_lights, include_grips)`: Get currently selected objects
7. `look_up_RhinoScriptSyntax(function_name)`: Look up RhinoScript documentation

## Reference Implementation

The reference implementation in Python can be found in the [rhino_mcp GitHub repository](https://github.com/reer-ide/rhino_mcp). Our C# implementation should maintain compatibility with this protocol.
