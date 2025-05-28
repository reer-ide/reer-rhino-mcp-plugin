# Debugging Guide for REER Rhino MCP Plugin

## Common Debugging Issues and Solutions

### Eto.dll Release Build Warnings

When debugging with "Launch Rhino and Attach Debugger", you may see warnings like:
```
You are debugging a Release build of Eto.dll. Using Just My Code with Release builds using compiler optimizations results in a degraded debugging experience (e.g. breakpoints will not be hit).
```

**This is normal and doesn't indicate a problem with your plugin.** Eto.dll is Rhino's UI framework and ships as an optimized release build.

### hostpolicy.dll Runtime Errors

If you encounter errors like:
```
A fatal error was encountered. The library 'hostpolicy.dll' required to execute the application was not found...
```

**Use the "Launch Rhino and Attach Debugger" configuration** - this launches Rhino properly as a separate process and avoids .NET runtime conflicts.

## Solutions

### Option 1: Disable "Just My Code" (Recommended)

1. In Visual Studio: **Tools** → **Options** → **Debugging** → **General**
2. **Uncheck** "Enable Just My Code (Managed only)"
3. Click **OK**

### Option 2: Use VS Code Debug Configuration (Recommended)

The project includes two working debug configurations:

1. **"Launch Rhino and Attach Debugger"** (Primary): 
   - Automatically launches Rhino using PowerShell script
   - Handles .NET runtime setup properly
   - Attaches debugger automatically
   - Provides installation instructions

2. **"Attach to Rhino Process"** (Manual): 
   - For attaching to an already running Rhino instance

Both configurations have:
- `"justMyCode": false` - Disables Just My Code warnings
- `"suppressJITOptimizations": true` - Better debugging experience
- `"enableStepFiltering": false` - Allows stepping through all code

### Option 3: Build in Debug Mode

Use the debug build configuration:
```bash
dotnet build --configuration Debug --framework net48
```

This ensures your plugin code is not optimized and includes full debug symbols.

## Debugging Workflow

### 1. Build and Install Plugin
```bash
# Build in debug mode
dotnet build --configuration Debug --framework net48

# Copy to Rhino plugins directory (optional - the launch script handles this)
Copy-Item "bin/Debug/net48/rhino_mcp_plugin.rhp" "$env:APPDATA/McNeel/Rhinoceros/8.0/Plug-ins/" -Force
```

### 2. Start Debugging Session

**Option A: Launch Rhino and Attach Debugger (Recommended)**
- Select **"Launch Rhino and Attach Debugger"** from VS Code debug panel
- Follow the on-screen instructions that appear in the terminal
- Rhino will launch automatically with proper environment setup
- Debugger attaches automatically once Rhino is ready

**Option B: Attach to Running Rhino**
- Start Rhino manually
- Load the plugin: `PlugInManager` → Load → Select your `.rhp` file
- Use **"Attach to Rhino Process"** configuration

### 3. Plugin Installation (First Time)

When using "Launch Rhino and Attach Debugger":
1. **Follow terminal instructions** - the script provides step-by-step guidance
2. In Rhino: **Tools** → **Options** → **Plugins**
3. Click **"Install..."** and navigate to your plugin directory
4. Select the `.rhp` file and install it
5. The debugger is already attached and ready

### 4. Set Breakpoints

Place breakpoints in your plugin code:
- `ReerRhinoMCPPlugin.OnLoad()` - Plugin initialization
- `RhinoMCPServer.StartAsync()` - Server startup
- `BasicCommandHandler.ProcessCommand()` - Command processing

### 5. Test Plugin Functionality

```bash
# In Rhino command line
RhinoMCP

# Test with Python client
python test_client.py
```

## Debugging Tips

### 1. Check Plugin Loading
Monitor Rhino's command line for plugin messages:
```
Loading REER Rhino MCP Plugin...
No saved RhinoMCP settings found, using defaults.
REER Rhino MCP Plugin loaded successfully
```

### 2. Enable Debug Logging
Add debug output to your code:
```csharp
if (settings.EnableDebugLogging)
{
    RhinoApp.WriteLine($"Debug: {message}");
}
```

### 3. Use Rhino's Output Window
- **View** → **Panels** → **Command History**
- Shows all `RhinoApp.WriteLine()` output

### 4. Handle Exceptions Gracefully
```csharp
try
{
    // Your code here
}
catch (Exception ex)
{
    RhinoApp.WriteLine($"Error: {ex.Message}");
    // Set breakpoint here to inspect exception
}
```

## Common Issues

### File Locked During Build
If you get "file is locked by Rhino" errors:
1. Close Rhino
2. Build the project
3. Restart Rhino

### Breakpoints Not Hit
1. Ensure you're building in Debug mode
2. Verify the `.pdb` file exists alongside your `.rhp` file
3. Check that "Suppress JIT optimizations" is enabled
4. Use "Launch Rhino and Attach Debugger" instead of direct launch

### Plugin Not Loading
1. Check Rhino version compatibility (.NET 4.8 for Rhino 8)
2. Verify all dependencies are available
3. Check for assembly loading errors in Rhino's command line
4. Ensure the plugin is installed in the correct directory

### Runtime Errors (hostpolicy.dll)
- **Always use "Launch Rhino and Attach Debugger"** instead of direct launch
- The PowerShell script (`start-rhino.ps1`) handles proper .NET runtime setup

## Performance Debugging

### Memory Usage
```csharp
// Monitor memory usage
var beforeGC = GC.GetTotalMemory(false);
// Your code here
var afterGC = GC.GetTotalMemory(true);
RhinoApp.WriteLine($"Memory used: {afterGC - beforeGC} bytes");
```

### Timing Operations
```csharp
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
// Your code here
stopwatch.Stop();
RhinoApp.WriteLine($"Operation took: {stopwatch.ElapsedMilliseconds}ms");
```

## Available VS Code Tasks

- **build-debug**: Build plugin in debug mode
- **build-release**: Build plugin in release mode  
- **install-plugin-debug**: Build and copy plugin to Rhino directory
- **launch-rhino**: Launch Rhino with proper environment (used by compound configuration)

## Troubleshooting Checklist

- [ ] Plugin builds without errors
- [ ] Debug symbols (.pdb) are generated
- [ ] Using "Launch Rhino and Attach Debugger" configuration
- [ ] Plugin loads in Rhino without errors
- [ ] Debugger attaches successfully
- [ ] Breakpoints are set in your plugin code (not Rhino/Eto code)
- [ ] "Just My Code" is disabled if needed
- [ ] Using Debug build configuration 