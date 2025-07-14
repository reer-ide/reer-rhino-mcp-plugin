# Implementation Guide

This guide outlines the implementation approach and best practices for developing the REER Rhino MCP Plugin.

## Directory Structure

The project follows this directory structure:

- `Commands/`: Rhino commands for the plugin
- `Core/`: Core implementations
  - `Server/`: Local TCP server implementation
  - `Client/`: WebSocket client for remote connections
  - `Common/`: Shared utilities and interfaces
  - `ComponentLibrary/`: Grasshopper component library management
    - `ComponentLibraryService.cs`: Main service for scanning and managing components
    - `Models/`: Data models for component information
    - `Storage/`: JSON serialization and file management
    - `Search/`: Component search and lookup functionality
- `Functions/`: MCP command handlers
  - `GrasshopperTools/`: Grasshopper-specific MCP tools
    - `LookupGHComponentsTool.cs`: Component search tool
    - `CreateGHComponentTool.cs`: Component creation tool
- `Serializers/`: JSON serialization utilities
- `UI/`: User interface components
- `Config/`: Configuration and settings management

## Connection Implementations

### Local TCP Server

```csharp
// Core/Server/RhinoMCPServer.cs
public class RhinoMCPServer : IRhinoMCPConnection
{
    // Implementation of local TCP server
    // Based on reference implementation
}
```

### Remote WebSocket Client

```csharp
// Core/Client/RhinoMCPClient.cs
public class RhinoMCPClient : IRhinoMCPConnection
{
    private WebSocket webSocket;
    private string serverUrl;
    
    public async Task ConnectAsync(string url, string token)
    {
        // Connect to remote server with authentication
    }
    
    // Implementation of WebSocket client
}
```

### Connection Manager

```csharp
// Core/RhinoMCPConnectionManager.cs
public class RhinoMCPConnectionManager
{
    private IRhinoMCPConnection activeConnection;
    
    public void SetConnectionMode(ConnectionMode mode, ConnectionSettings settings)
    {
        // Manages MCP connections and ensures only one is active at a time
    }
}
```

## Implementation Patterns

1. **Common Interface**

   Define a common interface for both connection types:

   ```csharp
   // Core/Common/IRhinoMCPConnection.cs
   public interface IRhinoMCPConnection
   {
       bool IsConnected { get; }
       Task<bool> StartAsync();
       Task StopAsync();
       event EventHandler<CommandReceivedEventArgs> CommandReceived;
       Task SendResponseAsync(string responseJson);
   }
   ```

2. **Component Library Service**

   Manage Grasshopper component library information:

   ```csharp
   // Core/ComponentLibraryService.cs
   public class ComponentLibraryService
   {
       private ComponentLibraryData _libraryData;
       private readonly string _cacheFilePath;
       
       public void Initialize()
       {
           // Check if library cache needs updating
           // Load existing cache or perform initial scan
       }
       
       public bool ShouldUpdateLibrary()
       {
           // Compare current library signature with cached version
           // Return true if libraries have changed
       }
       
       public void ScanAndStoreLibraries()
       {
           // Scan all loaded Grasshopper libraries
           // Extract component metadata
           // Store in local JSON cache
       }
       
       public ComponentSearchResult SearchComponents(string[] keywords, string category = null)
       {
           // Search cached components by keywords and category
           // Return ranked results with relevance scores
       }
       
       private string GenerateLibrarySignature()
       {
           // Create hash-based fingerprint of loaded libraries
           // Used for change detection
       }
   }
   ```

3. **Component Data Models**

   Define data structures for component information:

   ```csharp
   // Core/Models/ComponentLibraryData.cs
   public class ComponentLibraryData
   {
       public string Version { get; set; }
       public DateTime ScanDate { get; set; }
       public string RhinoVersion { get; set; }
       public string GrasshopperVersion { get; set; }
       public List<LibraryInfo> Libraries { get; set; }
   }
   
   public class LibraryInfo
   {
       public string Name { get; set; }
       public string Author { get; set; }
       public bool IsCoreLibrary { get; set; }
       public List<ComponentInfo> Components { get; set; }
   }
   
   public class ComponentInfo
   {
       public string Name { get; set; }
       public string ComponentGuid { get; set; }
       public string Category { get; set; }
       public string SubCategory { get; set; }
       public string Description { get; set; }
       public List<ParameterInfo> Inputs { get; set; }
       public List<ParameterInfo> Outputs { get; set; }
   }
   ```

4. **Partial Classes**
   
   Use partial classes to organize related functionality:

   ```csharp
   // Functions/GetRhinoSceneInfo.cs
   public partial class RhinoMCPFunctions
   {
       public JObject GetRhinoSceneInfo(JObject parameters)
       {
           // Implementation
       }
   }
   ```

5. **Thread Safety**

   Always use thread synchronization when accessing shared resources:

   ```csharp
   private readonly object lockObject = new object();
   
   private bool IsRunning()
   {
       lock (lockObject)
       {
           return running;
       }
   }
   ```

6. **Rhino UI Thread**

   Execute Rhino operations on the UI thread:

   ```csharp
   RhinoApp.InvokeOnUiThread(new Action(() =>
   {
       // Rhino operations here
   }));
   ```

7. **Error Handling**

   Use consistent error handling pattern:

   ```csharp
   try
   {
       // Operation
   }
   catch (Exception e)
   {
       return new JObject
       {
           ["status"] = "error",
           ["message"] = e.Message
       };
   }
   ```

8. **Configuration Management**

   Store and retrieve user settings:

   ```csharp
   // Config/RhinoMCPSettings.cs
   public class RhinoMCPSettings
   {
       public ConnectionMode Mode { get; set; }
       public string RemoteUrl { get; set; }
       
       public void Save()
       {
           // Save to Rhino plugin settings
       }
       
       public static RhinoMCPSettings Load()
       {
           // Load from Rhino plugin settings
       }
   }
   ```

## Debugging

This project is configured for debugging with Visual Studio Code.

### VS Code Setup

The `.vscode` directory contains two launch configurations:

1.  **`Rhino 8 - netcore`**: Launches Rhino 8, loads the .NET Core version of the plugin, and attaches the debugger. Use this for modern .NET development.
2.  **`Rhino 8 Windows - netfx`**: Launches Rhino 8, loads the .NET Framework 4.8 version of the plugin, and attaches the debugger. Use this for maximum compatibility with Rhino 8 on Windows.

### How to Debug

1.  **Select a Launch Configuration**: Open the "Run and Debug" view in VS Code (Ctrl+Shift+D) and select either the `netcore` or `netfx` configuration from the dropdown menu.
2.  **Set Breakpoints**: Place breakpoints in your C# code where you want the debugger to pause. For example, in `RhinoMCPServer.cs` to debug the server lifecycle.
3.  **Start Debugging**: Press **F5** or click the "Start Debugging" button.
4.  **Process**: VS Code will:
    *   Run the `build` task to compile the plugin.
    *   Launch Rhino 8.
    *   Attach the debugger to the `Rhino.exe` process.
5.  **Verify**: Once Rhino starts, your breakpoints should become active (solid red). You can then use the Rhino commands (e.g., `RhinoMCPServer`) to trigger your code and hit the breakpoints.

## Testing

Test each component in isolation:

1. Server connectivity
2. WebSocket client connectivity
3. Command parsing
4. Command execution
5. Response serialization
6. Authentication handling

## Reference Implementation

Use the reference implementation in the `docs/example` directory as a guide for the local server, but adapt it to match the MCP protocol from the original Python implementation and extend it to support remote connections.

## UI and Configuration Flow

For a seamless user experience, the plugin will manage configuration as follows:

1.  **First Run Detection**: On `Plugin.OnLoad`, the plugin will check if a configuration file or essential settings exist.
2.  **Configuration Dialog**: If it's the first run (or settings are invalid), an Eto-based dialog will be displayed, prompting the user to configure:
    *   **Connection Mode**: Local or Remote.
    *   **Local Settings**: Port number (e.g., 1999).
    *   **Remote Settings**: Server URL and an authentication token.
3.  **Settings Persistence**: The `RhinoMCPSettings` class will save these settings to Rhino's persistent settings storage. This avoids loose config files and integrates well with Rhino's infrastructure.
4.  **Manual Configuration**: The `RhinoReer` command will be extended with a `configure` option (e.g., `RhinoReer configure`) to allow users to open the settings dialog manually at any time.

## Resource Monitoring

To provide the AI with real-time context about the user's session, a monitoring service will be implemented.

### Event Handling

The service will subscribe to the following Rhino events:

-   `Rhino.Commands.Command.BeginCommand`: Log when a command starts.
-   `Rhino.Commands.Command.EndCommand`: Log the command's completion status and runtime.
-   `RhinoDoc.AddRhinoObject`: Triggered when a new object is created.
-   `RhinoDoc.DeleteRhinoObject`: Triggered when an object is deleted.
-   `RhinoDoc.ReplaceRhinoObject`: Triggered for modifications.
-   `RhinoDoc.ModifiedChanged`: A general event to catch other document changes.

### MCP Resource Structures

The monitoring service will populate data structures that are then exposed as MCP Resources.

**1. `command_history` (Temporary Resource)**

A capped-size, in-memory list of the most recent commands.

```json
{
  "resource_name": "command_history",
  "data": [
    {
      "command_name": "Box",
      "timestamp_start": "2023-10-27T10:00:05Z",
      "timestamp_end": "2023-10-27T10:00:10Z",
      "status": "success",
      "result_summary": "Created 1 box object."
    },
    {
      "command_name": "Move",
      "timestamp_start": "2023-10-27T10:00:15Z",
      "timestamp_end": "2023-10-27T10:00:18Z",
      "status": "success",
      "result_summary": "Moved 1 object."
    }
  ]
}
```

**2. `document_metadata` (Persistent Resource)**

Metadata about the current Rhino document.

```json
{
  "resource_name": "document_metadata",
  "data": {
    "file_name": "project_x.3dm",
    "file_path": "C:\\Users\\User\\Documents\\project_x.3dm",
    "object_count": 152,
    "layer_count": 12,
    "units": "millimeters"
  }
}
```

This service will run on a background thread to avoid blocking the Rhino UI thread.


