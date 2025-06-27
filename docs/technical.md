---
description: 
globs: 
alwaysApply: false
---
# Implementation Guide

This guide outlines the implementation approach and best practices for developing the REER Rhino MCP Plugin.

## Directory Structure

The project follows this directory structure:

- `Commands/`: Rhino commands for the plugin
- `Core/`: Core implementations
  - `Server/`: Local TCP server implementation
  - `Client/`: WebSocket client for remote connections
  - `Common/`: Shared utilities and interfaces
- `Functions/`: MCP command handlers
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
    private string authToken;
    
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

2. **Partial Classes**
   
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

3. **Thread Safety**

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

4. **Rhino UI Thread**

   Execute Rhino operations on the UI thread:

   ```csharp
   RhinoApp.InvokeOnUiThread(new Action(() =>
   {
       // Rhino operations here
   }));
   ```

5. **Error Handling**

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

6. **Configuration Management**

   Store and retrieve user settings:

   ```csharp
   // Config/RhinoMCPSettings.cs
   public class RhinoMCPSettings
   {
       public ConnectionMode Mode { get; set; }
       public string RemoteUrl { get; set; }
       public string AuthToken { get; set; }
       
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


