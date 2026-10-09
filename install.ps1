# Builds ClipShelf, installs it to %LOCALAPPDATA%\ClipShelf\app, adds a Start menu shortcut,
# turns on start-with-Windows, and launches it. History (history.db) lives one folder up and survives reinstalls.
$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'ClipShelf\app'

Get-Process ClipShelf -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

dotnet publish (Join-Path $PSScriptRoot 'src\ClipShelf.csproj') -c Release -r win-x64 --self-contained false -o $dest
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$exe = Join-Path $dest 'ClipShelf.exe'
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'ClipShelf' -Value "`"$exe`""

$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'ClipShelf.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $dest
$shortcut.Save()

Start-Process $exe
Write-Host "ClipShelf installed to $dest and running. Press Ctrl+Shift+V."
