---
description: 
globs: 
alwaysApply: false
---
# Project Overview

This project is a Rhino plugin that implements the Model Context Protocol (MCP) to enable communication between Rhino and AI assistants like Claude. The plugin replaces the previous Python script-based approach with a native Rhino plugin for better integration and user experience.

## Key Files

- `ReerRhinoMCPPlugin.cs`: The main plugin class that initializes and manages the plugin lifecycle.
- `Commands/RhinoMCPServerCommand.cs`: The Rhino command to start and stop the MCP server.
- `Core/RhinoMCPConnectionManager.cs`: The central class for managing connection state (local server or remote client).
- `Core/Server/RhinoMCPServer.cs`: The implementation for the local TCP server.
- `Core/Client/RhinoMCPClient.cs`: The placeholder for the remote WebSocket client.
- `Config/RhinoMCPSettings.cs`: Handles persistent plugin settings.

## Implementation Approach

The plugin supports two connection modes:
1.  A local TCP socket server that listens for MCP commands from clients like Claude Desktop.
2.  A remote WebSocket client that connects to a cloud-based MCP bridge.

It executes these commands in Rhino and returns the results back to the client. The project is built with a modular architecture to separate concerns for connectivity, command handling, and configuration.

## Development Roadmap

The development roadmap is detailed in [.cursor/scratchpad.md](mdc:.cursor/scratchpad.md), which outlines the phases and tasks for completing the plugin.

