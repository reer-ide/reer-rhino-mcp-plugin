# Logging System

This directory contains the centralized logging system for the Rhino MCP Plugin that respects debug settings.

## Overview

The new logging system provides conditional logging based on the `EnableDebugLogging` setting in `RhinoMCPSettings`. This helps reduce noise in the Rhino command line while still providing detailed information when needed for debugging.

## Key Features

- **Conditional Logging**: Only shows debug/info messages when `EnableDebugLogging` is enabled
- **Always Show Critical Messages**: Warnings and errors are always displayed
- **Multiple Log Levels**: Debug, Info, Warning, Error, Success
- **Easy Migration**: Helper methods to convert existing `RhinoApp.WriteLine()` calls
- **UI Integration**: Ready for integration with the LogViewModel for UI display

## Usage

### Basic Logging

```csharp
using ReerRhinoMCPPlugin.Core.Common;

// Debug messages (only shown when debug logging enabled)
Logger.Debug("Connection attempt starting...");

// Info messages (only shown when debug logging enabled)
Logger.Info("Processing user request...");

// Success messages (only shown when debug logging enabled)
Logger.Success("✓ Connection established successfully");

// Warnings (always shown)
Logger.Warning("⚠ Connection unstable, retrying...");

// Errors (always shown)
Logger.Error("✗ Failed to connect to server");
```

### Formatted Logging

```csharp
Logger.DebugFormat("Processing {0} objects in {1}ms", count, elapsedTime);
Logger.ErrorFormat("Failed to process file: {0}", filename);
```

### Easy Migration Helper

For quick migration of existing code:

```csharp
using ReerRhinoMCPPlugin.Core.Common;

// Old way:
// RhinoApp.WriteLine("Some message");

// New way (automatically detects message type):
"Some message".Log();

// Or use the smart logger:
"✓ Success message".LogSmart();  // Detected as Success
"✗ Error occurred".LogSmart();   // Detected as Error
"⚠ Warning message".LogSmart();  // Detected as Warning
```

## Log Levels Behavior

| Level | When Shown | Use Case |
|-------|------------|----------|
| `Debug` | Only when `EnableDebugLogging = true` | Detailed debugging information |
| `Info` | Only when `EnableDebugLogging = true` | General information |
| `Success` | Only when `EnableDebugLogging = true` | Success confirmations |
| `Warning` | Always | Important warnings that don't break functionality |
| `Error` | Always | Errors and failures |

## Configuration

Debug logging is controlled by the `EnableDebugLogging` property in `RhinoMCPSettings`:

```csharp
// Enable debug logging
ReerRhinoMCPPlugin.Instance.MCPSettings.EnableDebugLogging = true;
ReerRhinoMCPPlugin.Instance.MCPSettings.Save();

// Disable debug logging (default)
ReerRhinoMCPPlugin.Instance.MCPSettings.EnableDebugLogging = false;
ReerRhinoMCPPlugin.Instance.MCPSettings.Save();
```

## Migration Guide

### Step 1: Add Using Statement
```csharp
using ReerRhinoMCPPlugin.Core.Common;
```

### Step 2: Replace RhinoApp.WriteLine Calls

**Before:**
```csharp
RhinoApp.WriteLine("Connection established");
RhinoApp.WriteLine($"Error: {ex.Message}");
RhinoApp.WriteLine("[DEBUG] Processing request...");
```

**After:**
```csharp
Logger.Success("Connection established");
Logger.Error($"Error: {ex.Message}");
Logger.Debug("Processing request...");
```

### Step 3: Use Extension Methods for Quick Migration

**Quick replacement:**
```csharp
// Replace: RhinoApp.WriteLine(message);
// With:    message.Log();

"Connection established".Log();           // Auto-detected as Debug
"✓ Connection successful".Log();          // Auto-detected as Success  
"✗ Connection failed".Log();             // Auto-detected as Error
"⚠ Connection unstable".Log();           // Auto-detected as Warning
```

## Benefits

1. **Reduced Noise**: Users see fewer messages during normal operation
2. **Better Debugging**: Developers can enable detailed logging when needed
3. **Consistent Formatting**: All log messages have consistent level prefixes
4. **Future-Proof**: Ready for UI integration and advanced logging features
5. **Easy Migration**: Existing code can be updated incrementally

## Example Settings Usage

```csharp
// Enable debug logging for troubleshooting
var settings = ReerRhinoMCPPlugin.Instance.MCPSettings;
settings.EnableDebugLogging = true;
settings.Save();

Logger.Debug("This message will now be visible");
Logger.Info("This message will also be visible");
Logger.Warning("This message was always visible");
```