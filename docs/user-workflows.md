# User Workflows Guide

This guide describes the typical workflows for using the REER Rhino MCP Plugin with its advanced session management and auto-start features.

## Initial Setup Workflow

### First-Time Remote Connection Setup
1. **Install Plugin**: Plugin auto-loads on Rhino startup
2. **Register License**: Run `ReerLicense` to register for remote connections
3. **Start Remote Connection**: Run `ReerStart` → Select "Remote"
4. **Automatic Configuration**: Connection settings automatically saved for future use

### First-Time Local Connection Setup
1. **Install Plugin**: Plugin auto-loads on Rhino startup
2. **Start Local Server**: Run `ReerStart` → Select "Local" → Configure host/port
3. **Automatic Configuration**: Settings automatically saved for future use

## Daily Usage Workflows

### Seamless Remote Connection (Recommended)
1. **Start Rhino**: Plugin loads and connects automatically
2. **Work Normally**: Session persists across Rhino restarts
3. **Optional**: Use `ReerStop` to disconnect while preserving session
4. **Restart Rhino**: Automatically reconnects to existing session

### Local Connection for Claude Desktop
1. **Start Rhino**: Plugin loads and starts local server automatically
2. **Connect Claude Desktop**: Configure Claude to use `localhost:1999`
3. **Work with AI**: Direct MCP communication with Claude
4. **Stop When Done**: Run `ReerStop` to free the port

## Advanced Workflows

### Session Management

#### Preserve Session (Default for Remote)
```
ReerStop → Rhino Restart → Automatic reconnection to same session
```

#### Fresh Session Start
```
ReerRestart → Clean session with no cached data
```

#### Switch Connection Types
```
ReerStop → ReerStart → Select different mode → Settings automatically saved
```

### Troubleshooting Workflows

#### Connection Issues
1. **Check Status**: Run `ReerRhinoMCP` to see current connection status
2. **Fresh Start**: Run `ReerRestart` to clear any cached session issues
3. **License Issues**: Run `ReerLicense` to check/re-register license

#### Session Problems
1. **Clear Session**: Run `ReerRestart` to force fresh session
2. **File Integrity**: Plugin automatically validates file integrity for security
3. **Manual Restart**: Stop Rhino → Restart → Auto-connection resumes

## Command Reference

### Primary Commands
- **`ReerStart`**: Start connection, automatically save settings
- **`ReerStop`**: Stop connection, preserve remote sessions
- **`ReerRestart`**: Force fresh session and restart
- **`ReerLicense`**: Manage licenses for remote connections
- **`ReerRhinoMCP`**: Check status and manual control

### Workflow Examples

#### Typical Remote User (Set-and-Forget)
```
Day 1: ReerLicense → ReerStart (Remote) → Work
Day 2+: Start Rhino → Automatic connection → Work
```

#### Local Development User
```
Session: ReerStart (Local) → Connect Claude Desktop → Work → ReerStop
Next Session: Start Rhino → Server auto-starts → Continue
```

#### Mixed Usage
```
Morning: Rhino starts → Auto-connects to remote
Afternoon: ReerStop → ReerStart (Local) → Connect Claude Desktop
Evening: ReerStop → ReerStart (Remote) → Continue remote work
```

## Best Practices

### For Remote Connections
- **Let auto-start handle connections**: No need to manually start after initial setup
- **Use `ReerStop` for temporary disconnections**: Preserves your session context
- **Use `ReerRestart` for troubleshooting**: Forces fresh session when needed
- **Keep same Rhino file open**: Sessions are linked to files for security

### For Local Connections
- **Configure Claude Desktop once**: Plugin remembers your local server settings
- **Use `ReerStop` when not needed**: Frees up the TCP port for other applications
- **Check firewall settings**: Ensure localhost:1999 is accessible

### General Tips
- **Plugin auto-loads**: No need to manually load the plugin
- **Settings persist**: Successful connections automatically become defaults
- **Session security**: Remote sessions validate file integrity automatically
- **Multiple modes**: Switch between local and remote as needed

## Troubleshooting Common Issues

### "No connection" on startup
- Check if auto-start is enabled in settings
- Verify license status with `ReerLicense`
- Try `ReerRestart` for fresh connection

### Claude Desktop can't connect
- Ensure local server is running (`ReerStart` → Local)
- Check that port 1999 is not blocked by firewall
- Verify Claude Desktop MCP configuration

### Session lost after Rhino restart
- Sessions should automatically reconnect for remote connections
- If issues persist, try `ReerRestart` for fresh session
- Check that the same Rhino file is open (sessions are file-linked)

### License issues
- Run `ReerLicense` to check status
- Re-register license if machine fingerprint changed
- Contact support if license validation fails