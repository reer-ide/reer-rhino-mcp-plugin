# Rhino MCP Plugin

A Rhino 8 plugin developed using RhinoCommon.

## Debugging in Cursor

Since direct debugging launch doesn't work properly in Cursor, use the following steps to debug your plugin:

### Method 1: Build, Launch Rhino, Then Attach Debugger

1. Build the plugin:
   ```
   dotnet build
   ```

2. Launch Rhino using the PowerShell script:
   ```
   .\start-rhino.ps1
   ```

3. In Rhino, install the plugin:
   - Go to Tools > Options > Plugins
   - Click "Install..."
   - Navigate to the `bin\Debug` folder
   - Select the `.rhp` file and install it

4. In Cursor's Debug panel:
   - Select "Attach to Rhino"
   - Click the green start button
   - When prompted, select the Rhino process from the list

5. Set breakpoints in your code and use your plugin in Rhino to trigger them

### Method 2: Using the Compound Debug Configuration

1. In Cursor's Debug panel:
   - Select "Launch Rhino and Attach Debugger"
   - Click the green start button
   - This will build the project, launch Rhino, and prepare to attach the debugger
   - When prompted, select the Rhino process

2. Install the plugin in Rhino as described in Method 1, step 3

## Development Notes

- Use Visual Studio 2022 for direct debugging without the attach step
- The plugin should work with both .NET Framework and .NET Core runtimes