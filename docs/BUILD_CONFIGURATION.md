# Build Configuration Guide

This document explains the build configuration for the ReerConnector Rhino MCP Plugin.

## Build Configurations

### Debug Configuration
- **Purpose**: Development and testing
- **Features**:
  - Development mode: **ENABLED** (connects to development server)
  - Debug logging: **ENABLED** (verbose logging for troubleshooting)
  - All commands included: `ReerStart`, `ReerStop`, `ReerLicense`, `ReerUtils`, `ReerToggleDebugLogging`
  - Full debugging symbols
  - No code optimization

### Release Configuration
- **Purpose**: Production deployment
- **Features**:
  - Development mode: **DISABLED** (connects to production server)
  - Debug logging: **DISABLED** (minimal logging)
  - Production commands only: `ReerStart`, `ReerStop`, `ReerLicense`
  - Optimized code
  - Minimal debug symbols

## Building the Plugin

### Using Visual Studio Code
The default build task is configured for Debug mode:
- Press `Ctrl+Shift+B` (Windows/Linux) or `Cmd+Shift+B` (macOS)
- Or use Terminal → Run Build Task

Available tasks:
- `build` - Debug build (default)
- `build debug` - Explicit debug build
- `build release` - Release build

### Using dotnet CLI
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Build for specific framework
dotnet build -f net48
dotnet build -f net7.0
```

## Configuration Details

### Conditional Compilation Symbols

#### Debug Mode
- `DEBUG` - Standard debug symbol
- `TRACE` - Enable trace logging
- `DEV_MODE` - Enable development features

#### Release Mode
- `TRACE` - Enable trace logging
- `RELEASE` - Release mode indicator

### Settings Defaults

| Setting | Debug | Release |
|---------|-------|---------|
| EnableDebugLogging | true | false |
| DevelopmentMode | true | false |
| Server URL | dev-mcp.reer.dev | mcp.reer.dev |

### Excluded Commands in Release

The following commands are excluded from Release builds:
- `ReerRhinoMCPCommand` - Manual server control command
- `ReerUtilsCommand` - Utility commands for development
- `ReerToggleDebugLoggingCommand` - Debug logging toggle

## Building for Different Environments

### Local Development
```bash
# Build with development settings (default)
dotnet build
```
- Uses development server
- Enables debug logging
- Includes all commands

### Production Release
```bash
# Build for production deployment
dotnet build -c Release
```
- Uses production server
- Disables debug logging
- Excludes development commands

## Development Workflow

1. **During Development**: Use Debug configuration
   - Enables all debugging features
   - Connects to development server
   - All commands available

2. **Testing**: Use Debug configuration
   - Test with development server
   - Verify all features work

3. **Production Deployment**: Use Release configuration
   - Optimized for performance
   - Connects to production server
   - Only essential commands included

## Server URLs

- **Development**: `ws://127.0.0.1:8080` (this is for local testing now)
- **Production**: `https://mcp.reer.dev` (placeholder for actual production server)

The server URL is automatically selected based on the `DevelopmentMode` setting, which is controlled by the build configuration.