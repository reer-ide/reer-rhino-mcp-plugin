---
description: 
globs: 
alwaysApply: false
---
# Project Overview

This project is a Rhino plugin that implements the Model Context Protocol (MCP) to enable communication between Rhino and AI assistants like Claude. The plugin provides a comprehensive solution for AI-assisted CAD workflows with persistent connections, automatic startup, and session management.

## Key Files

### Core Plugin Infrastructure
- `ReerRhinoMCPPlugin.cs`: The main plugin class with auto-start functionality and lifecycle management
- `Core/RhinoMCPConnectionManager.cs`: Central connection manager with session persistence
- `Core/Server/RhinoMCPServer.cs`: Local TCP server implementation
- `Core/Client/RhinoMCPClient.cs`: Remote WebSocket client with license-based authentication
- `Config/RhinoMCPSettings.cs`: Persistent settings with auto-start and connection preferences

### Command Interface
- `Commands/ReerStartCommand.cs`: Start connections with automatic settings persistence
- `Commands/ReerStopCommand.cs`: Stop connections with optional session preservation
- `Commands/ReerRestartCommand.cs`: Restart with fresh session cleanup
- `Commands/ReerLicenseCommand.cs`: License management for remote connections
- `Commands/ReerRhinoMCPCommand.cs`: Main control interface

### MCP Function Library
- `Core/Functions/`: Complete set of MCP tools for Rhino manipulation
- `Core/ToolExecutor.cs`: Attribute-based function discovery and execution
- `Core/Client/LicenseManager.cs`: License validation and management
- `Core/Client/FileIntegrityManager.cs`: File integrity validation for secure sessions

## Implementation Approach

The plugin supports two connection modes with intelligent session management:

### 1. Local TCP Server Mode
- Direct connection for Claude Desktop and other MCP clients
- Listens on localhost:1999 by default
- Immediate response with no authentication required

### 2. Remote WebSocket Client Mode
- Connects to cloud-based MCP servers
- License-based authentication with machine fingerprinting
- Persistent sessions with automatic reconnection
- File integrity validation for secure operations

## Key Features

### Auto-Start & Persistence
- **Plugin auto-loads** on Rhino startup (`PlugInLoadTime.AtStartup`)
- **Automatic connection** to last successful configuration
- **Session persistence** for remote connections (preserves context between restarts)
- **Settings persistence** automatically saves successful connection configurations

### Session Management
- **Smart session handling**: `ReerStop` preserves sessions, `ReerRestart` cleans them
- **File integrity validation**: Ensures secure session management with file linking
- **Automatic reconnection**: Attempts to resume existing sessions when possible

### Security & Licensing
- **Machine fingerprinting**: Hardware-bound license validation
- **Encrypted storage**: Secure license and session data storage
- **File-based session linking**: Sessions tied to specific Rhino files for security

## Development Roadmap

The development roadmap is detailed in [.cursor/scratchpad.md](mdc:.cursor/scratchpad.md), which outlines the phases and tasks for completing the plugin.

