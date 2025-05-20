$rhinoPath = "C:\Program Files\Rhino 8\System\Rhino.exe"
$env:RHINO_PACKAGE_DIRS = Join-Path $PSScriptRoot "bin\Debug"
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
$env:DOTNET_MULTILEVEL_LOOKUP = "1"

# Print instructions
Write-Host "------------------------------------------------------------"
Write-Host "DEBUGGING INSTRUCTIONS:" -ForegroundColor Yellow
Write-Host "1. Rhino will start now with plugin directory: $env:RHINO_PACKAGE_DIRS"
Write-Host "2. In Rhino, go to Tools > Options > Plugins"
Write-Host "3. Click 'Install...' and navigate to:"
Write-Host "   $env:RHINO_PACKAGE_DIRS" -ForegroundColor Cyan
Write-Host "4. Select the .rhp file and install it"
Write-Host "5. Return to Cursor and select 'Attach to Rhino' from the Debug panel"
Write-Host "6. When prompted, select the Rhino process from the list"
Write-Host "------------------------------------------------------------"

# Start Rhino process
Start-Process -FilePath $rhinoPath -ArgumentList "/nosplash" 