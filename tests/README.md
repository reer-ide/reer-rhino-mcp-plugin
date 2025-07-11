# RhinoMCP Integration Testing

This directory contains comprehensive integration tests for the RhinoMCP plugin system.

## Test Files

- `test_client.py` - Comprehensive mock host application for testing all flows
- `test_multi_storey.3dm` - Sample Rhino file for testing (3MB)
- `requirements.txt` - Python dependencies for testing

## Setup

1. Install Python dependencies:
   ```bash
   pip install -r tests/requirements.txt
   ```

2. Ensure the remote MCP server is running:
   ```bash
   cd ../rhino_mcp_remote
   python -m remote_server.server
   ```

## Testing Process

### Method 1: VSCode Debugging (Recommended)

1. **Start Remote Server**: Make sure the remote MCP server is running on `localhost:8080`

2. **Debug with Rhino**: Press F5 in VSCode to start debugging
   - This will launch Rhino with the test file (`test_multi_storey.3dm`)
   - The plugin will be loaded automatically

3. **Run Test Client**: Use Ctrl+Shift+P → "Tasks: Run Task" → "run test client"
   - This runs the comprehensive test client that simulates the host app

### Method 2: Manual Testing

1. **Start Remote Server**:
   ```bash
   cd ../rhino_mcp_remote
   python -m remote_server.server
   ```

2. **Launch Rhino with Test File**:
   ```bash
   "C:\Program Files\Rhino 8\System\Rhino.exe" /netcore tests/test_multi_storey.3dm
   ```

3. **Run Test Client**:
   ```bash
   python tests/test_client.py
   ```

## Test Coverage

The test client simulates the complete host application behavior and tests:

### Phase 1: One-Time Initialization
- ✅ License ID generation
- ✅ Plugin installation simulation
- ✅ License registration with remote server
- ✅ Auth token reception and storage

### Phase 2: File Linking
- ✅ File selection and validation
- ✅ Local file hash calculation
- ✅ File size determination
- ✅ Session creation with remote server
- ✅ Session metadata storage

### Phase 3: Connection Establishment
- ✅ WebSocket connection setup
- ✅ Handshake protocol
- ✅ Message receiver threading
- ✅ Connection status management

### Phase 4: Command Testing
- ✅ Ping command
- ✅ Get Rhino info
- ✅ Get document info
- ✅ Create cube geometry
- ✅ Create sphere geometry

### Phase 5: File Integrity Management
- ✅ File status checking
- ✅ File validation and change detection
- ✅ Integrity monitoring

### Phase 6: Project Card Opening (Reconnection)
- ✅ File integrity validation
- ✅ Connection re-establishment
- ✅ Modified file detection
- ✅ Error handling for file changes

## Expected Results

When running the test successfully, you should see:

1. **Console Output**: Colorized step-by-step progress with success/error indicators
2. **Rhino Objects**: New cube and sphere objects created in the test file
3. **Rhino Console**: Command responses and debug information
4. **WebSocket Communication**: Real-time message exchange between client and server

## Troubleshooting

### Common Issues

1. **"Test file not found"**: Ensure `test_multi_storey.3dm` exists in the tests folder
2. **"Connection failed"**: Check that the remote server is running on port 8080
3. **"License registration failed"**: Verify the remote server's license endpoint is working
4. **"Plugin not loaded"**: Make sure the plugin is built and in the Debug folder

### Debug Information

- Check Rhino's command line for detailed plugin output
- Review the remote server logs for connection attempts
- Monitor WebSocket messages for communication issues
- Verify file permissions for the test .3dm file

## Architecture Testing

This test suite validates the enhanced architecture documented in `/docs/architecture.md`:

- **Separated UX Flow**: Initialization → File Linking → Connection
- **Client-side File Integrity**: Local hash calculation and validation
- **License-based Authentication**: Hardware fingerprinting and token management
- **Persistent Sessions**: Session creation and restoration
- **Auto-reconnection**: Project card opening and connection re-establishment

## Next Steps

After successful testing:

1. Verify all created objects in Rhino
2. Check the remote server logs for proper message handling
3. Test edge cases (file modification, connection loss, etc.)
4. Validate file integrity management in real scenarios 