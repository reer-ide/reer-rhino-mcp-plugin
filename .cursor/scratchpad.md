# REER Rhino MCP Plugin Development Scratchpad

This document keeps track of development tasks, notes, and progress for the REER Rhino MCP Plugin.

## Development Plan

### 1. Core Infrastructure (Initial Phase)

- [ ] **Socket Communication Layer**
  - [ ] Implement TCP socket server class
  - [ ] Set up thread management for client connections
  - [ ] Implement proper connection handling
  - [ ] Add error handling and logging
  - [ ] Test basic connectivity

- [ ] **MCP Protocol Implementation**
  - [ ] Define command/response format
  - [ ] Implement command routing system
  - [ ] Create serializer for Rhino objects
  - [ ] Create deserializer for client commands
  - [ ] Test protocol with sample commands

- [ ] **Plugin Commands**
  - [ ] Implement `RhinoMCP` command with UI
  - [ ] Implement `RhinoMCPConnect` command
  - [ ] Implement `RhinoMCPDisconnect` command
  - [ ] Create settings UI for configuration
  - [ ] Test commands in Rhino

### 2. Tool Implementation (Main Phase)

- [ ] **Scene Information Tools**
  - [ ] Implement `get_rhino_scene_info()`
  - [ ] Implement `get_rhino_layers()`
  - [ ] Implement `get_rhino_selected_objects(include_lights, include_grips)`
  - [ ] Implement `get_rhino_objects_with_metadata(filters, metadata_fields)`
  - [ ] Test with Claude Desktop

- [ ] **Visualization Tools**
  - [ ] Implement `capture_rhino_viewport(layer, show_annotations, max_size)`
  - [ ] Optimize image capture and encoding
  - [ ] Test viewport capture quality and performance

- [ ] **Execution Tools**
  - [ ] Implement `execute_code(code)`
  - [ ] Implement `look_up_RhinoScriptSyntax(function_name)`
  - [ ] Add sandboxing for code execution
  - [ ] Test execution safety and performance

### 3. Extended Functionality (Enhancement Phase)

- [ ] **Remote Connection Support**
  - [ ] Design token-based authentication
  - [ ] Implement secure connection to remote servers
  - [ ] Add connection status monitoring
  - [ ] Test remote connectivity

- [ ] **Configuration UI**
  - [ ] Design settings panel layout
  - [ ] Implement connection management UI
  - [ ] Create preferences storage system
  - [ ] Test settings persistence

- [ ] **Performance Optimization**
  - [ ] Optimize buffer management for large transfers
  - [ ] Enhance geometry serialization
  - [ ] Add caching for frequently accessed data
  - [ ] Performance testing with large models

### 4. Testing & Packaging (Final Phase)

- [ ] **Testing**
  - [ ] Write unit tests for core functionality
  - [ ] Perform integration testing with Claude Desktop
  - [ ] Test with various Rhino models and versions
  - [ ] Address any performance or stability issues

- [ ] **Documentation**
  - [ ] Update code documentation
  - [ ] Create user guide
  - [ ] Add developer documentation
  - [ ] Create sample scripts and examples

- [ ] **Packaging**
  - [ ] Prepare package for Rhino Package Manager
  - [ ] Create installer for manual installation
  - [ ] Generate release notes
  - [ ] Plan version upgrade path

## Implementation Notes

### Connection Modes

The plugin must support two distinct connection modes:

1. **Local TCP Server Mode**
   - Acts as a TCP server listening on localhost (default port 1999)
   - Used for direct communication with Claude Desktop and other local applications
   - Simple stdio-based protocol communication

2. **Remote WebSocket Client Mode**
   - Acts as a WebSocket client connecting to a remote MCP server
   - Initiates outbound connection to bypass NAT/firewall limitations
   - Uses authentication token for secure user identification
   - Maintains persistent connection to remote server
   - Enables integration with cloud-based AI services

### Socket Communication

- Default port: 1999 (for local mode)
- Need to ensure thread safety with Rhino's main thread
- Consider using a command queue for thread synchronization
- WebSocket client needs reconnection logic for reliability

### Remote Connection Architecture

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

- The plugin initiates a WebSocket connection to the remote server
- The remote server handles multiple client connections and routes commands
- Each client is identified by a unique authentication token
- Commands include user identification for proper routing

### UI Requirements

- Settings panel for configuring connection type
- For remote mode: inputs for WebSocket URL and authentication token
- Status indicator showing current connection state
- Clear user feedback and error messaging

### MCP Protocol

- Will need to follow [Model Context Protocol](https://modelcontextprotocol.io/quickstart/server) standards
- Command format should match the Python implementation
- Need to handle both direct commands (local mode) and routed commands (remote mode)

### Rhino Integration

- All Rhino operations must run on main thread via `RhinoApp.InvokeOnUiThread`
- Need to handle undo/redo properly with `BeginUndoRecord`/`EndUndoRecord`

## Resources

- [Rhino MCP GitHub Repository](https://github.com/reer-ide/rhino_mcp)
- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [RhinoCommon API Documentation](https://developer.rhino3d.com/api/RhinoCommon/html/R_Project_RhinoCommon.htm) 