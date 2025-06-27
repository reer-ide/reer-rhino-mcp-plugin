---
description: 
globs: 
alwaysApply: false
---
# Plugin Architecture

The REER Rhino MCP Plugin follows a modular architecture with clear separation of concerns, supporting both local and remote connections.

## Core Components

1. **Plugin Base**
   - `ReerRhinoMCPPlugin.cs`: Plugin entry point, initialization, and lifecycle management.

2. **UI and Configuration Layer**
   - **First-Time Setup UI**: A user-friendly dialog (Eto form) shown on the first run to configure connection settings (Local port, Remote URL/Token).
   - `RhinoMCPSettings`: Manages loading and saving of persistent settings to a local config file.
   - `RhinoReer` Command: Acts as the primary user interface for manually starting/stopping the server and checking status.

3. **Connection Components**
   - `RhinoMCPServer`: Local TCP socket server implementation. Handles client connections and exposes MCP Tools and Resources.
   - `RhinoMCPClient`: WebSocket client for remote connections.
   - `RhinoMCPConnectionManager`: Manages the active connection type.

4. **Resource Monitoring Service**
   - A background service that hooks into Rhino's application events (`BeginCommand`, `EndCommand`, `AddRhinoObject`, etc.).
   - Collects and structures contextual data (command history, document metadata).
   - Provides this data to the active connection's MCP Resources endpoint.

5. **Command Handlers (MCP Tools)**
   - Handlers for specific MCP commands (e.g., `create_box`, `get_document_info`).
   - These are the "Tools" that the AI model can execute.

6. **MCP Resources**
   - Read-only data endpoints exposed by the server.
   - Provides context to the AI (e.g., `command_history`, `document_metadata`).
   - Supports querying and potentially real-time subscriptions.

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
│  Rhino MCP   │◄─────►│  Remote MCP     │◄─────►│  AI Client   │
│  Plugin      │◄─── WS│  Server         │  API  │  (e.g. Claude)│
│ (WS Client)  │       │  (Bridge)       │       │              │
│              │       │                 │       │              │
└──────┬───────┘       └─────────────────┘       └──────────────┘
       │
       ▼
┌───────────────┐
│               │
│   Resource    │
│   Monitoring  │
│    Service    │
└───────────────┘
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

## Data and Control Flow

1.  **First Run**: The plugin shows a UI for the user to configure connection settings. Settings are saved locally.
2.  **Startup**: The plugin loads the settings. If auto-start is enabled, it initiates the chosen connection mode.
3.  **User Actions**: The **Resource Monitoring Service** listens to Rhino events (e.g., user runs a command, adds an object).
4.  **Context Update**: The service updates the **MCP Resources** (e.g., adds to `command_history`).
5.  **AI Interaction**:
    *   An external AI client queries the MCP Resources to get the latest context.
    *   Based on the context and user prompt, the AI decides to call an **MCP Tool** (a command).
    *   The `RhinoMCPServer` receives the command, the corresponding **Command Handler** executes it, and a result is returned.

