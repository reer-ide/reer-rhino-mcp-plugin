---
description: 
globs: 
alwaysApply: false
---
# Plugin Architecture

The REER Rhino MCP Plugin follows a modular architecture with clear separation of concerns, supporting both local and remote connections.

## Core Components

1. **Plugin Base**
   - `ReerRhinoMCPPlugin.cs`: Plugin entry point with auto-start functionality (`PlugInLoadTime.AtStartup`)
   - Automatic connection initialization with stored settings
   - Comprehensive lifecycle management and graceful shutdown

2. **Connection Management Layer**
   - `RhinoMCPConnectionManager`: Central coordinator with session persistence support
   - `RhinoMCPSettings`: Persistent settings with auto-start preferences and connection history
   - **Command Interface**: Multiple commands for different connection scenarios:
     - `ReerStart`: Start connections with automatic settings persistence
     - `ReerStop`: Stop with optional session preservation (preserves remote sessions by default)
     - `ReerRestart`: Force fresh session cleanup and restart
     - `ReerLicense`: License management for remote connections

3. **Connection Implementations**
   - `RhinoMCPServer`: Local TCP socket server implementation with multi-client support
   - `RhinoMCPClient`: Full WebSocket client with license authentication and session management
   - `LicenseManager`: Hardware-bound license validation with encrypted storage
   - `FileIntegrityManager`: File integrity validation for secure session linking

4. **Component Library Service**
   - **Purpose**: Manages Grasshopper component library information for AI-assisted component creation and lookup.
   - **Initialization**: Scans all loaded Grasshopper libraries on first launch and plugin startup.
   - **Storage**: Maintains local JSON cache of component metadata (names, descriptions, GUIDs, inputs/outputs).
   - **Update Detection**: Monitors for new/updated libraries and refreshes cache when changes detected.
   - **Search & Lookup**: Provides fast component search by keywords, categories, and functionality.
   - **MCP Integration**: Exposes component lookup through `look_up_gh_components` MCP tool.

5. **Resource Monitoring Service**
   - A background service that hooks into Rhino's application events (`BeginCommand`, `EndCommand`, `AddRhinoObject`, etc.).
   - Collects and structures contextual data (command history, document metadata).
   - Provides this data to the active connection's MCP Resources endpoint.

6. **Session Management System**
   - **Session Persistence**: Remote connections maintain session context across restarts
   - **File Integrity Validation**: Sessions linked to specific Rhino files for security
   - **Smart Stop Behavior**: Preserves sessions for remote connections, cleans for local
   - **Automatic Reconnection**: Attempts to resume existing sessions when available
   - **Fresh Session Support**: `ReerRestart` command forces clean session state

7. **Command Handlers (MCP Tools)**
   - Handlers for specific MCP commands via `ToolExecutor` with attribute-based discovery
   - **Core Functions**: Complete set in `Core/Functions/` (object creation, selection, modification)
   - **Grasshopper Tools**: `look_up_gh_components`, `create_gh_component` for AI-assisted workflows
   - **Scene Tools**: Viewport capture, document info, layer management
   - All tools marked with `MCPToolAttribute` for automatic discovery

8. **MCP Resources**
   - Read-only data endpoints exposed by the server
   - Real-time context updates from Resource Monitoring Service
   - Provides context to AI (command history, document metadata, object information)
   - Supports querying and real-time subscriptions

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

### Startup Sequence
1. **Plugin Auto-Load**: Plugin loads automatically on Rhino startup (`PlugInLoadTime.AtStartup`)
2. **Settings Initialization**: Load persistent settings including connection preferences
3. **Auto-Start Connection**: If enabled and valid settings exist, automatically connect after 2-second delay
4. **Session Restoration**: For remote connections, attempt to resume existing sessions if available

### Connection Lifecycle
1. **Manual Connection**: User runs `ReerStart` → selects mode → connection established → **settings saved**
2. **Session Management**: 
   - `ReerStop`: Disconnects but preserves session info for remote connections
   - `ReerRestart`: Forces session cleanup and fresh connection
3. **Automatic Reconnection**: Plugin attempts to resume sessions when restarting

### Runtime Operation
1. **Resource Monitoring**: Service listens to Rhino events (commands, object changes, etc.)
2. **Context Updates**: Real-time updates to MCP Resources (command history, document state)
3. **AI Interaction Flow**:
   - AI queries MCP Resources for current context
   - AI calls appropriate MCP Tools based on context and user requests
   - Tools execute via `ToolExecutor` with automatic function discovery
   - Results returned to AI with appropriate success/error handling

### Security & Validation
- **License Validation**: Remote connections validate license with machine fingerprinting
- **File Integrity**: Sessions linked to specific files with hash validation
- **Encrypted Storage**: License and session data stored securely

## Component Library Service Architecture

### Purpose & Scope
The Component Library Service provides AI systems with comprehensive knowledge of available Grasshopper components, enabling intelligent component selection and automated workflow creation.

### Core Functionality

#### 1. Library Scanning Process
- **Trigger Events**: First plugin launch, Grasshopper startup, new plugin installation
- **Scan Scope**: All loaded Grasshopper libraries via `Grasshopper.Instances.ComponentServer.Libraries`
- **Component Discovery**: Instantiates components to extract metadata (name, description, inputs, outputs, GUID)
- **Library Classification**: Distinguishes between core Grasshopper components and user-installed plugins

#### 2. Local Storage Strategy
- **Storage Location**: Plugin data directory (`%APPDATA%/McNeel/Rhinoceros/8.0/Plug-ins/ReerRhinoMCP/`)
- **File Format**: JSON structure with versioning support
- **Data Structure**:
  ```json
  {
    "version": "1.0",
    "scan_date": "2024-01-15T10:30:00Z",
    "rhino_version": "8.0",
    "grasshopper_version": "1.0",
    "libraries": [
      {
        "name": "Grasshopper",
        "is_core": true,
        "components": [
          {
            "name": "Box",
            "component_guid": "guid-string",
            "category": "Surface",
            "subcategory": "Primitive",
            "description": "Create a box aligned to world axes",
            "inputs": [...],
            "outputs": [...]
          }
        ]
      }
    ]
  }
  ```

#### 3. Update Detection Mechanism
- **Library Signature**: Hash-based fingerprint of loaded libraries (names, versions, component counts)
- **Change Detection**: Compare current signature with stored signature on startup
- **Incremental Updates**: Only re-scan changed libraries, not entire component set
- **Fallback Strategy**: Full re-scan if signature comparison fails

#### 4. AI Integration Flow
```
User Request: "Create a box with dimensions 5x3x2"
↓
AI calls: look_up_gh_components(keywords=["box", "rectangle"])
↓
Plugin searches local cache and returns matching components
↓
AI selects appropriate component and calls: create_gh_component(guid="...", params={...})
↓
Plugin creates component on Grasshopper canvas
```

### Performance Considerations
- **Lazy Loading**: Component metadata loaded on-demand during search
- **Search Optimization**: Indexed search by name, category, and keywords
- **Memory Management**: Cache frequently accessed components, cleanup unused data
- **Startup Impact**: Background scanning to avoid blocking plugin initialization

### Error Handling & Resilience
- **Graceful Degradation**: Continue operation even if some components fail to scan
- **Corruption Recovery**: Rebuild cache if JSON file is corrupted
- **Missing Libraries**: Handle cases where previously scanned libraries are uninstalled
- **Version Compatibility**: Maintain backward compatibility with older cache formats

