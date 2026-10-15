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
| Ctrl+P | Save (pin) / unsave |
| Tab, or click a tab | Switch between the All and Saved tabs |
| Ctrl+Del (or Del with empty search) | Delete clip |
| Esc | Close |

Right-click a card for the same actions. Left-click the tray icon to open the picker in copy-only mode. The tray menu has Pause capture, Start with Windows, Open data folder, Clear history (keeps pinned), and Quit.

## Data and settings

- `%LOCALAPPDATA%\ClipShelf\history.db`: captured clips. Copying the same thing again moves it to the top instead of making a duplicate. Clips you haven't used (pasted or copied) in 30 days are deleted automatically, at startup and then daily, unless they are saved (pinned).
- `%LOCALAPPDATA%\ClipShelf\settings.json`: `Hotkey` (e.g. `"Ctrl+Alt+V"`; restart to apply), `Paused`, and `RetentionDays` (default `30`; `0` turns automatic cleanup off; restart to apply).

### Privacy

ClipShelf saves clipboard history locally in an **unencrypted** SQLite database and keeps it until you delete it or it goes unused for `RetentionDays` (30 by default; saved clips are kept indefinitely). It does not upload or sync your clips. Pause capture from the tray menu before copying anything you do not want saved. Deleting a clip removes it from the app's history, but does not securely erase it from the database, write-ahead log, or backups. The tray's Clear history command also keeps pinned clips.

ClipShelf does **not** detect or filter passwords, API keys, access tokens, private keys, or other secrets. If you copy one, it may be saved in history. The only automatic exclusion is for copies that the source app explicitly marks as private (`ExcludeClipboardContentFromMonitorProcessing`, `Clipboard Viewer Ignore`, or `CanIncludeInClipboardHistory = 0`). Do not rely on every app to set those markers. Use Pause capture when you do not want a copy retained, and delete sensitive entries from history when you no longer need them.

## Build

.NET 8 SDK. `dotnet build src\ClipShelf.csproj`. `tools\make-icon.ps1` regenerates `src\icon.ico`.

## License

[MIT](LICENSE)
