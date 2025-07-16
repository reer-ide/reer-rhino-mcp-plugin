# Rhino Integration Guide

This guide explains how to properly integrate with Rhino's API for the REER Rhino MCP Plugin and how to use the plugin's advanced session management features.

## Plugin Commands

The plugin provides several commands for different use cases:

### Connection Commands
- **`ReerStart`**: Start a new connection (Local TCP server or Remote WebSocket client)
  - Automatically saves successful connection settings for future use
  - Supports both interactive prompts and direct execution
- **`ReerStop`**: Stop current connection
  - Preserves session info for remote connections (allows reconnection)
  - Cleans session info for local connections
- **`ReerRestart`**: Restart with fresh session
  - Forces session cleanup and starts a new connection
  - Useful when you need to clear all cached session data

### Management Commands
- **`ReerLicense`**: License management for remote connections
- **`ReerRhinoMCP`**: Main control interface with status information

## Auto-Start Behavior

The plugin automatically:
1. **Loads on Rhino startup** (no manual loading required)
2. **Connects to last successful configuration** after a 2-second delay
3. **Preserves session context** for remote connections across Rhino restarts
4. **Saves connection settings** automatically when `ReerStart` succeeds

## Session Management

### For Remote Connections
- Sessions are **persistent** across Rhino restarts
- **File integrity validation** ensures secure session management
- **Automatic reconnection** to existing sessions when possible
- Use `ReerRestart` when you need a completely fresh session

### For Local Connections
- Sessions are **temporary** and cleaned when stopping
- No session persistence (stateless connections)
- Each `ReerStart` creates a fresh TCP server instance

## Key Rhino APIs

1. **RhinoApp**
   - `RhinoApp.WriteLine()`: Display messages in Rhino's command line
   - `RhinoApp.InvokeOnUiThread()`: Execute code on Rhino's UI thread

2. **RhinoDoc**
   - `RhinoDoc.ActiveDoc`: Access the current document
   - `doc.Objects`: Access geometric objects in the document
   - `doc.Layers`: Access document layers

3. **Command System**
   - Inherit from `Rhino.Commands.Command` for custom commands
   - Override `EnglishName` and `RunCommand` methods

4. **Undo System**
   - `doc.BeginUndoRecord("Description")`: Start recording changes
   - `doc.EndUndoRecord(int)`: Finish recording changes

## Threading Considerations

Rhino's API is not thread-safe. Always use `RhinoApp.InvokeOnUiThread()` when calling Rhino APIs from background threads:

```csharp
RhinoApp.InvokeOnUiThread(new Action(() =>
{
    var doc = RhinoDoc.ActiveDoc;
    // Perform operations on doc
}));
```

## Object Serialization

When serializing Rhino objects for MCP commands:

1. Use RhinoCommon's geometry methods to extract necessary information
2. Avoid serializing entire Rhino objects, which may contain circular references
3. Convert points, curves, and meshes to simplified JSON representations

## Plugin Lifecycle

The plugin should:

1. Initialize cleanly when loaded by Rhino
2. Stop all threads and release resources when unloaded
3. Handle Rhino shutdown gracefully

Add these methods to the plugin class:

```csharp
protected override void OnShutdown()
{
    // Stop server and clean up resources
    base.OnShutdown();
}
```
