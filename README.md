# REER Rhino MCP Plugin

A production-ready Rhino plugin that provides seamless AI-assisted 3D modeling through the Model Context Protocol (MCP). Features automatic startup, persistent sessions, and comprehensive Rhino integration for professional CAD workflows.

## Overview

The REER Rhino MCP Plugin is the official implementation of the [Rhino MCP project](https://github.com/reer-ide/rhino_mcp), designed to provide enterprise-grade AI integration for Rhino users. This plugin offers automatic connection management, persistent sessions, and comprehensive MCP tool library for professional 3D modeling workflows.

### Key Features

- **🚀 Auto-Start**: Plugin loads automatically on Rhino startup and connects to your preferred configuration
- **🔄 Session Persistence**: Remote connections maintain context across Rhino restarts
- **🔐 Enterprise Security**: License-based authentication with machine fingerprinting and encrypted storage
- **⚡ Smart Session Management**: Intelligent session preservation and cleanup options
- **🛠️ Complete MCP Library**: Comprehensive set of tools for object creation, manipulation, and scene analysis
- **🌐 Dual Connection Modes**: Support for both local (Claude Desktop) and remote (cloud) connections
- **💾 Automatic Configuration**: Successful connections automatically saved as defaults
- **🔧 Advanced Commands**: Multiple commands for different workflow scenarios

## Installation

### Via Rhino Package Manager (Recommended)

1. In Rhino, go to **Tools** > **Package Manager**
2. Search for "REER Rhino MCP"
3. Click **Install**
4. Restart Rhino when prompted

### Manual Installation

1. Download the latest release from the [Releases page](https://github.com/reer-ide/reer-rhino-mcp-plugin/releases)
2. In Rhino, go to **Tools** > **Options** > **Plugins**
3. Click **Install...** and select the downloaded .rhp file
4. Restart Rhino when prompted

## Usage

### Quick Start (Auto-Connection)

**For most users, no setup is needed after installation:**

1. Install the plugin (see Installation section above)
2. **Plugin automatically loads and connects** when you start Rhino
3. Begin working with AI - the connection is ready!

### First-Time Setup

#### For Remote Connections (Recommended)
1. Register your license: `ReerLicense`
2. Start remote connection: `ReerStart` → Select "Remote"
3. **Settings automatically saved** - future Rhino sessions will auto-connect

#### For Local Connections (Claude Desktop)
1. Start local server: `ReerStart` → Select "Local" → Configure host/port
2. **Settings automatically saved** - server will auto-start on future Rhino sessions
3. Configure Claude Desktop to connect to your server (default: localhost:1999)

### Daily Workflow

**The plugin is designed for "set-and-forget" operation:**

- **Start Rhino** → Plugin auto-loads and connects automatically
- **Work normally** → AI assistance available immediately
- **Restart Rhino** → Automatically reconnects to your sessions

### Available Commands

- **`ReerStart`**: Start new connection (auto-saves settings for future use)
- **`ReerStop`**: Stop connection (preserves session for remote connections)
- **`ReerRestart`**: Force fresh session cleanup and restart
- **`ReerLicense`**: Manage licenses for remote connections
- **`ReerRhinoMCP`**: Check status and manual control

## Testing

To test the plugin without Claude AI:

1. Start the server: `RhinoReer` → `local_start`
2. Run the test client: `python test_client.py`
3. The test client will connect and send sample commands to verify the server is working

## Session Management

The plugin provides intelligent session management for uninterrupted workflows:

### For Remote Connections
- **Persistent Sessions**: Sessions maintain context across Rhino restarts
- **File-Linked Security**: Sessions are securely linked to your current Rhino file
- **Automatic Reconnection**: Plugin attempts to resume existing sessions
- **Smart Preservation**: `ReerStop` preserves sessions, `ReerRestart` cleans them

### For Local Connections
- **Stateless Operation**: Each session is independent
- **Port Management**: Server automatically starts/stops as needed
- **Claude Desktop Integration**: Seamless connection with Claude Desktop

## Example Interactions

Once connected, you can ask Claude AI to perform complex CAD operations:

### Basic Modeling
- "Create a cube at the origin with side length 10"
- "Make a sphere with radius 5 at point (10, 0, 0)"
- "Draw a line from (0,0,0) to (10,10,5)"

### Advanced Operations
- "Create a parametric spiral staircase with 20 steps"
- "Generate a lattice structure for this organic form"
- "Optimize this geometry for 3D printing"

### Analysis & Inspection
- "Calculate the total surface area of all objects on layer 'Walls'"
- "Analyze the structural integrity of this beam"
- "Create a section view through the building at Y=50"

### Scene Management
- "Organize objects by material type into separate layers"
- "Create a rendered viewport showing the south elevation"
- "Export all curves on the 'Construction' layer to DXF"

## Configuration

Advanced users can configure the plugin by editing the config file located at:
- Windows: `%APPDATA%\REER\RhinoMCP\config.json`
- macOS: `~/Library/Application Support/REER/RhinoMCP/config.json`

## Development

This plugin is built using RhinoCommon with modern architecture patterns including auto-start, session management, and comprehensive MCP tool library.

### Architecture Highlights

- **Auto-Start System**: Plugin loads automatically using `PlugInLoadTime.AtStartup`
- **Session Persistence**: File-linked sessions with integrity validation
- **Modular MCP Tools**: Attribute-based function discovery with `MCPToolAttribute`
- **Multi-Target Support**: .NET Framework 4.8 and .NET 7.0 for Rhino compatibility
- **Enterprise Security**: License validation with machine fingerprinting

### Prerequisites

- Visual Studio 2022 or JetBrains Rider
- .NET Framework 4.8 SDK (for Rhino 7/8 .NET Framework support)
- .NET 7.0 SDK (for Rhino 8 .NET Core support)
- Rhino 7 or 8 installed

### Building from Source

```bash
# Clone the repository
git clone https://github.com/reer-ide/reer-rhino-mcp-plugin.git
cd reer-rhino-mcp-plugin

# Build for both target frameworks
dotnet build

# Build release version
dotnet build --configuration Release
```

Output files:
- `bin/Debug/net48/rhino_mcp_plugin.rhp` (Rhino 7/8 .NET Framework)
- `bin/Debug/net7.0/rhino_mcp_plugin.rhp` (Rhino 8 .NET Core)

### Debugging

#### Visual Studio / Rider
1. Open `rhino_mcp_plugin.csproj`
2. Use configured launch profiles:
   - `Rhino 8 - netcore`: Debug with .NET Core runtime
   - `Rhino 8 - netfx`: Debug with .NET Framework runtime
3. Set breakpoints and press F5

#### VS Code
1. Open project in VS Code
2. Use `.vscode/launch.json` configurations
3. Select appropriate debug configuration and start debugging

## Troubleshooting

### Common Issues

#### Plugin doesn't auto-connect on startup
- Check that auto-start is enabled (default: enabled)
- Verify settings with `ReerRhinoMCP` command
- Try `ReerRestart` to force fresh connection

#### Claude Desktop can't connect to local server
- Ensure local server is running: `ReerStart` → "Local"
- Check firewall settings for port 1999
- Verify Claude Desktop MCP configuration points to `localhost:1999`

#### Remote connection license issues
- Run `ReerLicense` to check license status
- Re-register license if machine fingerprint changed
- Contact support if validation fails

#### Session lost after Rhino restart
- For remote connections, sessions should auto-reconnect
- Ensure the same Rhino file is open (sessions are file-linked)
- Try `ReerRestart` if auto-reconnection fails

### Getting Help

For detailed troubleshooting and workflow guides, see:
- [User Workflows Guide](docs/user-workflows.md)
- [Architecture Documentation](docs/architecture.md)
- [Rhino Integration Guide](docs/rhino-integration.md)

## Contributing

We welcome contributions to the REER Rhino MCP Plugin! To contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under a modified BSD 3-Clause License with additional protections for REER branding - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- This plugin is built upon the [Rhino MCP project](https://github.com/reer-ide/rhino_mcp)
- Special thanks to the Rhino development community
- Inspired by [Blender MCP](https://github.com/Anthropic-Labs/blender-mcp)

## Contact

For questions, feedback, or support, please contact:
- Email: support@reer.co
- Website: [https://reer.co](https://reer.co)
- GitHub Issues: [Report a bug](https://github.com/reer-ide/reer-rhino-mcp-plugin/issues)