# ClipShelf

A Paste-style clipboard history for Windows. It runs in the tray and saves everything you copy (text, rich text, links, images, files) permanently to a local SQLite database. Press **Ctrl+Shift+V** to open a strip of cards along the bottom of the screen, then pick a clip to paste it into the app you were using.

## Install

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

This builds the app into `%LOCALAPPDATA%\ClipShelf\app`, adds a Start menu shortcut, turns on start-with-Windows, and launches it. Run it again to update. History survives reinstalls.

## Keys (picker open)

| Key | Action |
| --- | --- |
| Type | Search all history (text and source app; every word must match) |
| Left / Right, mouse wheel | Move between cards |
| Enter / double-click | Paste |
| Shift+Enter | Paste as plain text |
| Ctrl+1 to Ctrl+9 | Paste that card |
| Ctrl+C | Copy without pasting |
| Ctrl+P | Pin / unpin |
| Tab | Toggle All / Pinned |
| Ctrl+Del (or Del with empty search) | Delete clip |
| Esc | Close |

Right-click a card for the same actions. Left-click the tray icon to open the picker in copy-only mode. The tray menu has Pause capture, Start with Windows, Open data folder, Clear history (keeps pinned), and Quit.

## Data and settings

- `%LOCALAPPDATA%\ClipShelf\history.db`: every clip, kept forever. Copying the same thing again moves it to the top instead of making a duplicate.
- `%LOCALAPPDATA%\ClipShelf\settings.json`: `Hotkey` (e.g. `"Ctrl+Alt+V"`; restart to apply) and `Paused`.
- Copies that apps mark as private, like those from password managers (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory = 0`), are never saved.

## Build

.NET 8 SDK. `dotnet build src\ClipShelf.csproj`. `tools\make-icon.ps1` regenerates `src\icon.ico`.
