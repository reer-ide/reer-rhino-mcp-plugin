# REER Rhino MCP Plugin

A Rhino plugin that connects Rhino to AI applications through the Model Context Protocol (MCP), allowing for AI-assisted 3D modeling and scene manipulation. This plugin simplifies the connection process by eliminating the need for manual script loading, making it accessible to all Rhino users.

## Overview

The REER Rhino MCP Plugin is an official plugin implementation of the [Rhino MCP project](https://github.com/reer-ide/rhino_mcp), designed to provide a seamless connection between Rhino and AI models like Claude through the Model Context Protocol. This plugin replaces the previous method of loading a TCP server via RhinoScript, offering a more user-friendly installation and configuration experience.

### Key Features

- **Easy Installation**: Install directly from the Rhino Package Manager
- **One-Click Connection**: Connect to Claude AI and other MCP-compatible AI models with a single command
- **Local and Remote Connections**: Connect to both local and remote hosted MCP servers
- **Configuration UI**: Simple interface for managing connection settings
- **Automatic Updates**: Stay current with the latest features and improvements

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

### Quick Start

1. Install the plugin (see Installation section above)
2. In Rhino, type the command `RhinoReer`
3. Enter `local_start` to start the local MCP server
4. The server will start on localhost:1999 and be ready for connections

### Connecting to Claude AI Desktop

1. Install [Claude AI Desktop](https://claude.ai/desktop)
2. Configure Claude Desktop to connect to the local MCP server (localhost:1999)
3. In Rhino, start the server with `RhinoReer` → `local_start`
4. In Claude Desktop, you can now interact with Rhino by asking it to create, modify, or analyze 3D models

### Available Commands

- `RhinoReer`: Main command to start/stop the MCP server
  - `local_start`: Start the local TCP server
  - `stop`: Stop the running server
  - `status`: Show current server status

## Testing

To test the plugin without Claude AI:

1. Start the server: `RhinoReer` → `local_start`
2. Run the test client: `python test_client.py`
3. The test client will connect and send sample commands to verify the server is working

## Example Interactions

Once connected, you can ask Claude AI to:

- "Create a cube at the origin with side length 10"
- "Change the color of the selected objects to red"
- "Calculate the total surface area of all objects on layer 'Walls'"
- "Create a parametric spiral staircase"
- "Analyze the structural integrity of this beam"
- "Optimize this form for minimal material usage"

## Configuration

Advanced users can configure the plugin by editing the config file located at:
- Windows: `%APPDATA%\REER\RhinoMCP\config.json`
- macOS: `~/Library/Application Support/REER/RhinoMCP/config.json`

## Development

This plugin is built using RhinoCommon and targets both Rhino 7 and Rhino 8.

### Prerequisites

- Visual Studio 2022 or Rider
- .NET Framework 4.8 SDK (for Rhino 7)
- .NET 7.0 SDK (for Rhino 8)
- Rhino 7 or 8 installed

### Building from Source

1. Clone this repository
2. Open `rhino_mcp_plugin.sln` in Visual Studio or Rider
3. Build the solution
4. The compiled plugin will be in the `bin/Debug` or `bin/Release` folder

### Debugging

#### In Visual Studio

1. Set `rhino_mcp_plugin` as the startup project
2. In project properties, set the debug executable to Rhino.exe
3. Press F5 to launch Rhino with the debugger attached

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