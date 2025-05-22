---
description: 
globs: 
alwaysApply: false
---
# Plugin Architecture

The REER Rhino MCP Plugin follows a modular architecture with clear separation of concerns, supporting both local and remote connections.

## Core Components

1. **Plugin Base**
   - [ReerRhinoMCPPlugin.cs](mdc:ReerRhinoMCPPlugin.cs): Plugin entry point and initialization

2. **Connection Components**
   - `RhinoMCPServer`: Local TCP socket server implementation
   - `RhinoMCPClient`: WebSocket client for remote connections
   - `RhinoMCPConnectionManager`: Manages both connection types

3. **Command Handlers**
   - Each MCP command will have a dedicated handler method
   - Commands will be organized in a partial class approach

4. **Serialization Layer**
   - Serializers for converting Rhino objects to JSON
   - Deserializers for converting JSON to command parameters

5. **Rhino Commands**
   - Rhino command implementations to manage connections
   - Commands to configure the plugin settings

6. **UI Components**
   - Settings panels for configuring connection preferences
   - Status indicators for connection state

## Connection Modes

The plugin supports two distinct connection modes:

### Local TCP Server Mode
```
┌───────────────┐       ┌───────────────┐
│               │       │               │
│  Claude       │◄─────►│  Rhino MCP    │
│  Desktop      │  TCP  │  Plugin       │
│  (MCP Client) │       │  (TCP Server) │
│               │       │               │
└───────────────┘       └───────────────┘
```

### Remote WebSocket Client Mode
```
┌──────────────┐       ┌─────────────────┐       ┌──────────────┐
│              │       │                 │       │              │
│  Rhino MCP   │◄─────►│  Remote MCP     │◄─────►│  Web App     │
│  Plugin      │   WS  │  Server         │  SSE  │  Backend     │
│  (WebSocket  │       │  (Connection    │       │  (MCP        │
│   Client)    │       │   Bridge)       │       │   Client)    │
│              │       │                 │       │              │
└──────────────┘       └─────────────────┘       └──────────────┘
```

## Component Interactions

The flow of execution is:

1. Plugin initializes with preferred connection mode
2. For local mode:
   - TCP server listens for incoming connections
   - Client connects and sends JSON commands
   - Commands are processed and responses returned
3. For remote mode:
   - WebSocket client connects to remote server
   - Client authenticates with token
   - Commands from remote server are processed
   - Results are sent back through WebSocket

## Threading Model

- Socket/WebSocket communication runs on background threads
- Command handlers execute on Rhino's main thread via `RhinoApp.InvokeOnUiThread`
- Thread synchronization is managed with locks and queues

## Error Handling

- All operations are wrapped in try/catch blocks
- Errors are logged and returned as JSON error responses
- Automatic reconnection logic for WebSocket client
- The plugin maintains stability even when connections fail

