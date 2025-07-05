# Rhino Integration Guide

This guide explains how to properly integrate with Rhino's API for the REER Rhino MCP Plugin.

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
